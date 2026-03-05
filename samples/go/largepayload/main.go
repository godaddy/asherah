package main

import (
	"context"
	"crypto/rand"
	"crypto/sha256"
	"encoding/hex"
	"encoding/json"
	"flag"
	"fmt"
	"os"
	"path/filepath"
	"runtime"
	"strconv"
	"strings"
	"time"

	"github.com/godaddy/asherah/go/appencryption"
	"github.com/godaddy/asherah/go/appencryption/pkg/crypto/aead"
	"github.com/godaddy/asherah/go/appencryption/pkg/kms"
	"github.com/godaddy/asherah/go/appencryption/pkg/persistence"
)

const (
	product     = "enclibrary"
	service     = "asherah"
	partitionID = "largepayload_test"
	staticKey   = "thisIsAStaticMasterKeyForTesting"
	mb          = 1024 * 1024
)

type testResult struct {
	sizeMB      int
	passed      bool
	encryptTime time.Duration
	decryptTime time.Duration
	err         error
}

func main() {
	sizes := flag.String("sizes", "25,50,100", "comma-separated payload sizes in MB")
	outDir := flag.String("outdir", "", "output directory for DRR files (default: temp dir)")
	keep := flag.Bool("keep", false, "retain output files after test")
	flag.Parse()

	sizeList, err := parseSizes(*sizes)
	if err != nil {
		fmt.Fprintf(os.Stderr, "invalid sizes: %v\n", err)
		os.Exit(1)
	}

	dir := *outDir
	if dir == "" {
		dir, err = os.MkdirTemp("", "largepayload-*")
		if err != nil {
			fmt.Fprintf(os.Stderr, "failed to create temp dir: %v\n", err)
			os.Exit(1)
		}

		if !*keep {
			defer os.RemoveAll(dir)
		}
	}

	fmt.Printf("Output directory: %s\n\n", dir)

	results := make([]testResult, 0, len(sizeList))

	for _, sizeMB := range sizeList {
		r := runTest(sizeMB, dir)
		results = append(results, r)
	}

	printSummary(results)

	for _, r := range results {
		if !r.passed {
			os.Exit(1)
		}
	}
}

func runTest(sizeMB int, outDir string) testResult {
	fmt.Printf("=== Large Payload Test: %d MB ===\n", sizeMB)

	result := testResult{sizeMB: sizeMB}

	// Step 1: Generate random payload
	payloadSize := sizeMB * mb

	genStart := time.Now()
	payload := make([]byte, payloadSize)
	if _, err := rand.Read(payload); err != nil {
		result.err = fmt.Errorf("generate: %w", err)
		fmt.Printf("  Generate:    FAILED (%v)\n\n", result.err)

		return result
	}

	fmt.Printf("  Generate:    %d MB (%v)\n", sizeMB, time.Since(genStart).Round(time.Millisecond))

	// Step 2: Checksum original data
	origHash := sha256.Sum256(payload)
	origHashHex := hex.EncodeToString(origHash[:])
	fmt.Printf("  SHA-256:     %s\n", origHashHex)

	// Step 3: Set up Asherah session
	crypto := aead.NewAES256GCM()

	km, err := kms.NewStatic(staticKey, crypto)
	if err != nil {
		result.err = fmt.Errorf("kms setup: %w", err)
		fmt.Printf("  Setup:       FAILED (%v)\n\n", result.err)

		return result
	}

	defer km.Close()

	metastore := persistence.NewMemoryMetastore()
	config := &appencryption.Config{
		Policy:  appencryption.NewCryptoPolicy(),
		Product: product,
		Service: service,
	}

	factory := appencryption.NewSessionFactory(config, metastore, km, crypto)
	defer factory.Close()

	sess, err := factory.GetSession(partitionID)
	if err != nil {
		result.err = fmt.Errorf("get session: %w", err)
		fmt.Printf("  Session:     FAILED (%v)\n\n", result.err)

		return result
	}

	defer sess.Close()

	ctx := context.Background()

	// Step 4: Encrypt
	encStart := time.Now()

	drr, err := sess.Encrypt(ctx, payload)
	if err != nil {
		result.err = fmt.Errorf("encrypt: %w", err)
		fmt.Printf("  Encrypt:     FAILED (%v)\n\n", result.err)

		return result
	}

	result.encryptTime = time.Since(encStart)
	fmt.Printf("  Encrypt:     %v\n", result.encryptTime.Round(time.Millisecond))

	// Step 5: Validate ciphertext
	if err := validateCiphertext(drr.Data); err != nil {
		result.err = fmt.Errorf("ciphertext validation: %w", err)
		fmt.Printf("  Ciphertext:  FAILED (%v)\n\n", result.err)

		return result
	}

	fmt.Printf("  Ciphertext:  OK (%d bytes, not null/zero)\n", len(drr.Data))

	// Step 6: Save DRR to file
	drrPath := filepath.Join(outDir, fmt.Sprintf("%dmb_drr.json", sizeMB))

	drrBytes, err := json.Marshal(drr)
	if err != nil {
		result.err = fmt.Errorf("marshal DRR: %w", err)
		fmt.Printf("  DRR save:    FAILED (%v)\n\n", result.err)

		return result
	}

	if err := os.WriteFile(drrPath, drrBytes, 0600); err != nil {
		result.err = fmt.Errorf("write DRR: %w", err)
		fmt.Printf("  DRR save:    FAILED (%v)\n\n", result.err)

		return result
	}

	fileSizeMB := float64(len(drrBytes)) / float64(mb)
	fmt.Printf("  DRR file:    %s (%.1f MB)\n", drrPath, fileSizeMB)

	// Free the original encrypted DRR from memory before loading from file
	drr = nil
	drrBytes = nil

	// Step 7: Load DRR from file and decrypt
	loadedBytes, err := os.ReadFile(drrPath)
	if err != nil {
		result.err = fmt.Errorf("read DRR: %w", err)
		fmt.Printf("  Decrypt:     FAILED (%v)\n\n", result.err)

		return result
	}

	var loadedDRR appencryption.DataRowRecord
	if err := json.Unmarshal(loadedBytes, &loadedDRR); err != nil {
		result.err = fmt.Errorf("unmarshal DRR: %w", err)
		fmt.Printf("  Decrypt:     FAILED (%v)\n\n", result.err)

		return result
	}

	loadedBytes = nil

	decStart := time.Now()

	decrypted, err := sess.Decrypt(ctx, loadedDRR)
	if err != nil {
		result.err = fmt.Errorf("decrypt: %w", err)
		fmt.Printf("  Decrypt:     FAILED (%v)\n\n", result.err)

		return result
	}

	result.decryptTime = time.Since(decStart)
	fmt.Printf("  Decrypt:     %v (from file round-trip)\n", result.decryptTime.Round(time.Millisecond))

	// Step 8: Verify checksum
	decHash := sha256.Sum256(decrypted)
	if origHash != decHash {
		result.err = fmt.Errorf("checksum mismatch: original=%s decrypted=%s",
			origHashHex, hex.EncodeToString(decHash[:]))
		fmt.Printf("  Verify:      FAILED (%v)\n\n", result.err)

		return result
	}

	fmt.Printf("  Verify:      PASS (checksums match)\n")

	// Step 9: Memory stats
	var m runtime.MemStats
	runtime.ReadMemStats(&m)

	fmt.Printf("  Memory:      HeapInuse=%dMB, Sys=%dMB\n\n",
		m.HeapInuse/uint64(mb), m.Sys/uint64(mb))

	result.passed = true

	return result
}

func validateCiphertext(data []byte) error {
	if len(data) == 0 {
		return fmt.Errorf("ciphertext is empty")
	}

	// Check head and tail for all zeros
	checkSize := min(1024, len(data))

	allZero := true

	for i := range checkSize {
		if data[i] != 0 {
			allZero = false

			break
		}
	}

	if allZero {
		// Also check the tail
		for i := len(data) - checkSize; i < len(data); i++ {
			if data[i] != 0 {
				allZero = false

				break
			}
		}
	}

	if allZero {
		return fmt.Errorf("ciphertext appears to be all zeros (%d bytes)", len(data))
	}

	return nil
}

func parseSizes(s string) ([]int, error) {
	parts := strings.Split(s, ",")
	sizes := make([]int, 0, len(parts))

	for _, p := range parts {
		p = strings.TrimSpace(p)

		n, err := strconv.Atoi(p)
		if err != nil {
			return nil, fmt.Errorf("invalid size %q: %w", p, err)
		}

		if n <= 0 {
			return nil, fmt.Errorf("size must be positive: %d", n)
		}

		sizes = append(sizes, n)
	}

	return sizes, nil
}

func printSummary(results []testResult) {
	fmt.Println("=== Summary ===")

	for _, r := range results {
		if r.passed {
			fmt.Printf("  %3d MB:  PASS  (encrypt: %v, decrypt: %v)\n",
				r.sizeMB,
				r.encryptTime.Round(time.Millisecond),
				r.decryptTime.Round(time.Millisecond))
		} else {
			fmt.Printf("  %3d MB:  FAIL  (%v)\n", r.sizeMB, r.err)
		}
	}

	allPassed := true

	for _, r := range results {
		if !r.passed {
			allPassed = false

			break
		}
	}

	if allPassed {
		fmt.Println("\n  Overall: PASS")
	} else {
		fmt.Println("\n  Overall: FAIL")
	}
}
