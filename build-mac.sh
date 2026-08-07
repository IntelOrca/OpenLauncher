#!/bin/bash
#Modified from Avalonia's documentation: https://docs.avaloniaui.net/docs/deployment/macos

set -e
trap 'echo "Error packaging app"; exit 1' ERR

PROJECT_NAME="openlauncher"
OUTPUT_DIR="./src/openlauncher/bin/Release/net10.0"
FINAL_OUTPUT_DIR="./src/openlauncher/bin/Release/net10.0/macos-universal"

# Function to build for a specific architecture
build_for_arch() {
    local arch=$1
    echo "Building for $arch..."
    dotnet publish -r osx-$arch -c Release
}

# Build for both architectures
build_for_arch "x64"
build_for_arch "arm64"

# Create the final output directory
mkdir -p "$FINAL_OUTPUT_DIR"

ARM_OUTPUT="$OUTPUT_DIR/osx-arm64/publish"
X64_OUTPUT="$OUTPUT_DIR/osx-x64/publish"

#Copy libraries into final output dir
for FILE in "$ARM_OUTPUT"/*; do
	BASENAME=$(basename "$FILE")
	if [ -f "$FILE" ] && [[ "$BASENAME" != "openlauncher" ]]; then
		cp "$FILE" "$FINAL_OUTPUT_DIR"/.
	fi
done

# Create universal binary
echo "Creating universal binary..."
lipo -create \
    "$OUTPUT_DIR/osx-x64/publish/$PROJECT_NAME" \
    "$OUTPUT_DIR/osx-arm64/publish/$PROJECT_NAME" \
    -output "$FINAL_OUTPUT_DIR/$PROJECT_NAME"
