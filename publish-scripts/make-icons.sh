#!/bin/bash
# Script to create icon files for Windows (.ico) and macOS (.icns) from a PNG image
# Usage: ./make-icons.sh path/to/source/image.png

# Check if ImageMagick is installed
if ! command -v convert &> /dev/null; then
    echo "ImageMagick is required but not installed. Please install it first."
    echo "On macOS: brew install imagemagick"
    echo "On Linux: sudo apt-get install imagemagick (or equivalent)"
    exit 1
fi

# Check argument
if [ $# -ne 1 ]; then
    echo "Usage: $0 path/to/source/image.png"
    echo "Example: $0 ../images/shrinkeng.png"
    exit 1
fi

SOURCE_IMAGE="$1"
BASENAME=$(basename "$SOURCE_IMAGE" | cut -d. -f1)
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
IMAGES_DIR="$SCRIPT_DIR/../images"

echo "Creating icons from $SOURCE_IMAGE..."

# Create Windows ICO file
echo "Creating Windows ICO file..."
convert "$SOURCE_IMAGE" -define icon:auto-resize=16,32,48,64,128,256 "$IMAGES_DIR/$BASENAME.ico"
echo "Created $IMAGES_DIR/$BASENAME.ico"

# Create macOS ICNS file
echo "Creating macOS ICNS file..."

# Create temporary iconset directory
ICONSET_DIR="/tmp/$BASENAME.iconset"
mkdir -p "$ICONSET_DIR"

# Generate the various icon sizes needed for macOS
sips -z 16 16     "$SOURCE_IMAGE" --out "$ICONSET_DIR/icon_16x16.png"
sips -z 32 32     "$SOURCE_IMAGE" --out "$ICONSET_DIR/icon_16x16@2x.png"
sips -z 32 32     "$SOURCE_IMAGE" --out "$ICONSET_DIR/icon_32x32.png"
sips -z 64 64     "$SOURCE_IMAGE" --out "$ICONSET_DIR/icon_32x32@2x.png"
sips -z 128 128   "$SOURCE_IMAGE" --out "$ICONSET_DIR/icon_128x128.png"
sips -z 256 256   "$SOURCE_IMAGE" --out "$ICONSET_DIR/icon_128x128@2x.png"
sips -z 256 256   "$SOURCE_IMAGE" --out "$ICONSET_DIR/icon_256x256.png"
sips -z 512 512   "$SOURCE_IMAGE" --out "$ICONSET_DIR/icon_256x256@2x.png"
sips -z 512 512   "$SOURCE_IMAGE" --out "$ICONSET_DIR/icon_512x512.png"
sips -z 1024 1024 "$SOURCE_IMAGE" --out "$ICONSET_DIR/icon_512x512@2x.png"

# Convert the iconset to icns
iconutil -c icns "$ICONSET_DIR" -o "$IMAGES_DIR/$BASENAME.icns"

# Clean up
rm -rf "$ICONSET_DIR"

echo "Created $IMAGES_DIR/$BASENAME.icns"
echo "Icon creation complete!"
