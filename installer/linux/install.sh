#!/usr/bin/env bash
set -euo pipefail

INSTALL_DIR="$HOME/.local/share/organizatodo-app"
APPLICATIONS_DIR="$HOME/.local/share/applications"

echo "Installing OrganizaTodo to $INSTALL_DIR..."
mkdir -p "$INSTALL_DIR"
cp -a ./payload/. "$INSTALL_DIR/"
chmod +x "$INSTALL_DIR/OrganizaTodo.Desktop"

mkdir -p "$APPLICATIONS_DIR"
sed "s#%h/.local/share/organizatodo-app#$INSTALL_DIR#g" ./organizatodo.desktop > "$APPLICATIONS_DIR/organizatodo.desktop"

echo "Installed. Launch OrganizaTodo from your applications menu, or run:"
echo "  $INSTALL_DIR/OrganizaTodo.Desktop"
