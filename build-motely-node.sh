#!/usr/bin/env bash
# Minimal build script for motely-node (AOT addon)
set -e

echo "Building motely-node (AOT addon)..."

# Clean
rm -rf motely-node/bin 2>/dev/null || true

# Build both platforms
echo "Publishing win-x64..."
dotnet publish Motely.NodeAddon/Motely.NodeAddon.csproj -c Release -r win-x64

echo "Publishing linux-x64..."
dotnet publish Motely.NodeAddon/Motely.NodeAddon.csproj -c Release -r linux-x64

# Pack
cd motely-node
echo "Creating tarball..."
npm pack

echo "Done. Tarball ready in motely-node/"
ls -lh motely-node-*.tgz
