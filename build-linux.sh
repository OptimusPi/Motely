#!/usr/bin/env bash
# Build linux-x64 in Docker (Ubuntu 22.04 = glibc 2.35 = Vercel-safe).
# Run from MotelyJAML repo root.
# Requires: Docker
set -e
rm -rf \
  Motely/obj \
  Motely.Orchestration/obj \
  Motely.NodeAddon/obj
src="$(pwd)"
docker build -f Dockerfile.linux-node -t motely-linux-node .
docker run --rm -v "$src:/src" -w /src motely-linux-node
echo "Done. Check motely-node/bin/linux-x64/Motely.NodeAddon.node"
