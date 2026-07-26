#!/bin/bash
# Fully removes iTunes-RPC: running process, LaunchAgent, and the
# installed .app bundle. Mirrors the Windows uninstaller's scope.
set -uo pipefail

APP_NAME="iTunes-RPC.app"
INSTALL_DIR="$HOME/Applications"
LABEL="com.hvtim.itunes-rpc"
PLIST_PATH="$HOME/Library/LaunchAgents/$LABEL.plist"

echo "Uninstalling iTunes-RPC..."

pkill -f "$INSTALL_DIR/$APP_NAME" 2>/dev/null || true
sleep 0.3

if [ -f "$PLIST_PATH" ]; then
    launchctl unload "$PLIST_PATH" 2>/dev/null || true
    rm -f "$PLIST_PATH"
    echo "Removed autostart LaunchAgent."
fi

if [ -d "$INSTALL_DIR/$APP_NAME" ]; then
    rm -rf "$INSTALL_DIR/$APP_NAME"
    echo "Removed $INSTALL_DIR/$APP_NAME."
fi

echo ""
echo "iTunes-RPC has been fully uninstalled."
echo "Nothing else on this machine was changed - Discord and Music.app themselves are untouched."
