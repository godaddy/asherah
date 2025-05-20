use bzip2::read::BzDecoder;
use flate2::read::GzDecoder;
use glob::glob;
use std::fs::File;
use std::io::{self, BufReader, Read, Seek, SeekFrom};
use std::path::{Path, PathBuf};
use std::sync::{Arc, Mutex};

use crate::FileReader;

/// GzipFile reader for gzip-compressed files
pub struct GzipFile {
    inner: Mutex<GzDecoder<BufReader<File>>>,
    path: PathBuf,
}

impl GzipFile {
    pub fn new(path: impl AsRef<Path>) -> io::Result<Self> {
        let file = File::open(&path)?;
        let reader = BufReader::new(file);
        let decoder = GzDecoder::new(reader);

        Ok(Self {
            inner: Mutex::new(decoder),
            path: path.as_ref().to_path_buf(),
        })
    }
}

impl FileReader for GzipFile {
    fn read(&mut self, buf: &mut [u8]) -> io::Result<usize> {
        let mut inner = self.inner.lock().unwrap();
        inner.read(buf)
    }

    fn reset(&mut self) -> io::Result<()> {
        let file = File::open(&self.path)?;
        let reader = BufReader::new(file);
        let decoder = GzDecoder::new(reader);
        *self.inner.lock().unwrap() = decoder;
        Ok(())
    }

    fn close(&mut self) -> io::Result<()> {
        // GzDecoder doesn't have a close method,
        // it will be closed when dropped
        Ok(())
    }
}

/// Bzip2File reader for bzip2-compressed files
pub struct Bzip2File {
    inner: Mutex<BzDecoder<BufReader<File>>>,
    path: PathBuf,
}

impl Bzip2File {
    pub fn new(path: impl AsRef<Path>) -> io::Result<Self> {
        let file = File::open(&path)?;
        let reader = BufReader::new(file);
        let decoder = BzDecoder::new(reader);

        Ok(Self {
            inner: Mutex::new(decoder),
            path: path.as_ref().to_path_buf(),
        })
    }
}

impl FileReader for Bzip2File {
    fn read(&mut self, buf: &mut [u8]) -> io::Result<usize> {
        let mut inner = self.inner.lock().unwrap();
        inner.read(buf)
    }

    fn reset(&mut self) -> io::Result<()> {
        let file = File::open(&self.path)?;
        let reader = BufReader::new(file);
        let decoder = BzDecoder::new(reader);
        *self.inner.lock().unwrap() = decoder;
        Ok(())
    }

    fn close(&mut self) -> io::Result<()> {
        // BzDecoder doesn't have a close method,
        // it will be closed when dropped
        Ok(())
    }
}

/// PlainFile reader for uncompressed files
pub struct PlainFile {
    inner: Mutex<BufReader<File>>,
}

impl PlainFile {
    pub fn new(path: impl AsRef<Path>) -> io::Result<Self> {
        let file = File::open(path)?;
        let reader = BufReader::new(file);

        Ok(Self {
            inner: Mutex::new(reader),
        })
    }
}

impl FileReader for PlainFile {
    fn read(&mut self, buf: &mut [u8]) -> io::Result<usize> {
        let mut inner = self.inner.lock().unwrap();
        inner.read(buf)
    }

    fn reset(&mut self) -> io::Result<()> {
        let mut inner = self.inner.lock().unwrap();
        inner.seek(SeekFrom::Start(0))?;
        Ok(())
    }

    fn close(&mut self) -> io::Result<()> {
        // BufReader doesn't have a close method,
        // the file will be closed when dropped
        Ok(())
    }
}

/// MultiFileReader for reading from multiple files sequentially
pub struct MultiFileReader {
    readers: Vec<Arc<dyn FileReader>>,
    current_index: usize,
}

impl MultiFileReader {
    pub fn new(readers: Vec<Arc<dyn FileReader>>) -> Self {
        Self {
            readers,
            current_index: 0,
        }
    }
}

impl FileReader for MultiFileReader {
    fn read(&mut self, buf: &mut [u8]) -> io::Result<usize> {
        if self.current_index >= self.readers.len() {
            return Ok(0);
        }

        let mut reader = self.readers[self.current_index].clone();
        let result = Arc::get_mut(&mut reader).unwrap().read(buf);

        if let Ok(0) = result {
            // End of current file, move to next one
            self.current_index += 1;
            if self.current_index < self.readers.len() {
                return self.read(buf);
            }
        }

        result
    }

    fn reset(&mut self) -> io::Result<()> {
        for reader in &mut self.readers {
            let mut r = reader.clone();
            Arc::get_mut(&mut r).unwrap().reset()?;
        }
        self.current_index = 0;
        Ok(())
    }

    fn close(&mut self) -> io::Result<()> {
        for reader in &mut self.readers {
            let mut r = reader.clone();
            Arc::get_mut(&mut r).unwrap().close()?;
        }
        Ok(())
    }
}

/// Open files matching a glob pattern
pub fn open_files_glob(pattern: impl AsRef<Path>) -> io::Result<Arc<dyn FileReader>> {
    let pattern = pattern.as_ref();
    let paths: Vec<PathBuf> = glob(pattern.to_str().unwrap())
        .map_err(|e| io::Error::new(io::ErrorKind::InvalidInput, e))?
        .filter_map(Result::ok)
        .collect();

    if paths.is_empty() {
        return Err(io::Error::new(
            io::ErrorKind::NotFound,
            format!("{:?} not found", pattern),
        ));
    }

    open_files(&paths)
}

/// Open a list of files
pub fn open_files(files: &[PathBuf]) -> io::Result<Arc<dyn FileReader>> {
    let mut readers = Vec::with_capacity(files.len());

    for path in files {
        let reader: Arc<dyn FileReader> =
            if path.extension().and_then(|ext| ext.to_str()) == Some("gz") {
                Arc::new(GzipFile::new(path)?)
            } else if path.extension().and_then(|ext| ext.to_str()) == Some("bz2") {
                Arc::new(Bzip2File::new(path)?)
            } else {
                Arc::new(PlainFile::new(path)?)
            };

        readers.push(reader);
    }

    Ok(Arc::new(MultiFileReader::new(readers)))
}
