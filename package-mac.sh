#!/bin/bash
#Modified from Avalonia's documentation: https://docs.avaloniaui.net/docs/deployment/macos
set -e
trap 'echo "Error packaging app"; exit 1' ERR

APP_NAME="./src/openlauncher/bin/Release/net10.0/macos-universal/OpenLauncher.app"
PUBLISH_OUTPUT_DIRECTORY="./src/openlauncher/bin/Release/net10.0/macos-universal/."

INFO_PLIST="./info.plist"

ICON_FILE="./src/openlauncher/resources/logo-mac.icns"

if [ -d "$APP_NAME" ]
then
    rm -rf "$APP_NAME"
fi

echo "Creating OpenLauncher.app"

rm -r -f "$APP_NAME"
mkdir "$APP_NAME"

mkdir "$APP_NAME/Contents"
mkdir "$APP_NAME/Contents/MacOS"
mkdir "$APP_NAME/Contents/Resources"

cp "$INFO_PLIST" "$APP_NAME/Contents/Info.plist"
cp "$ICON_FILE" "$APP_NAME/Contents/Resources/AppIcon.icns"
for FILE in "$PUBLISH_OUTPUT_DIRECTORY"/*; do
	if [ -f "$FILE" ]; then
		cp -a "$FILE" "$APP_NAME/Contents/MacOS/."
	fi
done

#Get version number from csproj
VERSION_NUMBER=$(sed -n 's/.*<AssemblyVersion>\(.*\)<\/AssemblyVersion>.*/\1/p' ./src/openlauncher/openlauncher.csproj)
#Replace placeholder version with real version number
sed -i -e "s/APP_VERSION_NUMBER/$VERSION_NUMBER/" "$APP_NAME/Contents/Info.plist"
#For whatever reason, sed on macOS creates a backup file when replacing text, so we need to remove it
rm  "$APP_NAME/Contents/Info.plist-e"

codesign --sign - --force --deep "./src/openlauncher/bin/Release/net10.0/macos-universal/OpenLauncher.app"

mkdir -p "$PUBLISH_OUTPUT_DIRECTORY/publish"

echo "Zipping OpenLauncher.app..."

ditto -c -k --keepParent "./src/openlauncher/bin/Release/net10.0/macos-universal/OpenLauncher.app" "./src/openlauncher/bin/Release/net10.0/macos-universal/publish/OpenLauncher.zip"
