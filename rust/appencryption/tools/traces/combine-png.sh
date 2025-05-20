#!/bin/bash
# Combine multiple PNG files into a grid
# Usage: ./combine-png.sh out/*.png

OUTPUT="combined.png"
COLS=2

if [ $# -eq 0 ]; then
    echo "Usage: $0 file1.png file2.png [file3.png...]"
    exit 1
fi

# Check if ImageMagick is installed
if ! command -v convert &> /dev/null; then
    echo "This script requires ImageMagick to be installed."
    echo "Please install it with your package manager, e.g.:"
    echo "  brew install imagemagick  # on macOS"
    echo "  apt-get install imagemagick  # on Debian/Ubuntu"
    exit 1
fi

if [ $# -eq 1 ]; then
    # Just copy the single file
    cp "$1" "$OUTPUT"
else
    # Calculate optimal grid
    NUM_FILES=$#
    ROWS=$(( (NUM_FILES + COLS - 1) / COLS ))
    
    # Use ImageMagick to create the grid
    convert "$@" -set option:distort:viewport "%[fx:w*$COLS]x%[fx:h*$ROWS]" \
        -distort SRT "%[fx:floor(i/$COLS)*w],%[fx:(i%$COLS)*h]" \
        -background none -layers merge +repage "$OUTPUT"
fi

echo "Created $OUTPUT with a ${COLS}x${ROWS} grid"