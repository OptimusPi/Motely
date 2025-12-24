# Publishing Guide: Balatro Seed Oracle MCP Server

## 🎯 Distribution Strategy

### 1. Docker Hub (Primary Distribution)

**Why:** Easiest for users, works everywhere

**Steps:**
1. Create Docker Hub account
2. Create repository: `balatro-seed-oracle-mcp`
3. Build and push:

```bash
# Build
docker build -f Motely.API/Dockerfile -t balatro-seed-oracle-mcp:latest ..

# Tag for Docker Hub
docker tag balatro-seed-oracle-mcp:latest yourusername/balatro-seed-oracle-mcp:latest
docker tag balatro-seed-oracle-mcp:latest yourusername/balatro-seed-oracle-mcp:v1.0.0

# Push
docker push yourusername/balatro-seed-oracle-mcp:latest
docker push yourusername/balatro-seed-oracle-mcp:v1.0.0
```

**Auto-build:** Set up Docker Hub automated builds from GitHub

### 2. GitHub Releases (Standalone Binaries)

**Why:** For users who don't want Docker

**Steps:**
1. Create GitHub Actions workflow (see below)
2. Tag release: `git tag v1.0.0 && git push --tags`
3. GitHub Actions builds and uploads binaries
4. Create GitHub Release with binaries

**Workflow:** `.github/workflows/release-mcp.yml`

### 3. npm Package (Wrapper)

**Why:** Easy install for Node.js users

**Steps:**
1. Create npm account
2. Publish:

```bash
cd Motely.API
npm publish
```

**Note:** This is just a wrapper that calls Docker or binary

### 4. MCP Server Directories

**Why:** Discoverability

**List on:**
- [MCP Servers Directory](https://github.com/modelcontextprotocol/servers) (if exists)
- [Awesome MCP](https://github.com/awesome-mcp) (if exists)
- Your own README with "Installation" section

## 📋 Pre-Publishing Checklist

### Code
- [ ] All tests pass
- [ ] No hardcoded paths
- [ ] Configuration via environment variables
- [ ] Error handling is robust
- [ ] Logging is appropriate

### Documentation
- [ ] README.md with installation instructions
- [ ] Docker README
- [ ] Claude Desktop setup guide
- [ ] Usage examples
- [ ] Troubleshooting guide

### Docker
- [ ] Dockerfile builds successfully
- [ ] Image size is reasonable (<500MB)
- [ ] Health check works
- [ ] Volumes are properly configured
- [ ] Environment variables documented

### Binaries
- [ ] Builds for Windows/Linux/macOS
- [ ] Self-contained (no .NET runtime needed)
- [ ] Tested on target platforms
- [ ] Signing (optional, for security)

### Metadata
- [ ] Version number updated
- [ ] Changelog created
- [ ] License file included
- [ ] Contributing guidelines (if open source)

## 🚀 Publishing Steps

### Step 1: Prepare Repository

```bash
# Create new repository (or use existing)
git init
git add .
git commit -m "Initial release: Balatro Seed Oracle MCP Server"
git remote add origin https://github.com/yourusername/balatro-seed-oracle-mcp.git
git push -u origin main
```

### Step 2: Docker Hub

1. Create repository on Docker Hub
2. Build and push:

```bash
docker build -f Motely.API/Dockerfile -t yourusername/balatro-seed-oracle-mcp:latest ..
docker push yourusername/balatro-seed-oracle-mcp:latest
```

3. Set up automated builds (optional)

### Step 3: GitHub Releases

1. Create release workflow (see below)
2. Tag and push:

```bash
git tag v1.0.0
git push --tags
```

3. GitHub Actions builds binaries
4. Create release on GitHub with binaries

### Step 4: npm (Optional)

```bash
cd Motely.API
npm publish
```

### Step 5: Announce

- Post on Reddit (r/balatro, r/programming)
- Share on Twitter/X
- Post on Discord servers
- Add to MCP server directories

## 📝 GitHub Actions Workflow

Create `.github/workflows/release-mcp.yml`:

```yaml
name: Release MCP Server

on:
  push:
    tags:
      - 'v*'

jobs:
  build:
    runs-on: ${{ matrix.os }}
    strategy:
      matrix:
        os: [ubuntu-latest, windows-latest, macos-latest]
        include:
          - os: ubuntu-latest
            runtime: linux-x64
          - os: windows-latest
            runtime: win-x64
          - os: macos-latest
            runtime: osx-x64

    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '10.0.x'

      - name: Publish
        run: |
          dotnet publish Motely.API/Motely.API.csproj \
            -c Release \
            -r ${{ matrix.runtime }} \
            -o publish/${{ matrix.runtime }} \
            -p:PublishSingleFile=true \
            -p:SelfContained=true

      - name: Package
        run: |
          cd publish/${{ matrix.runtime }}
          if [ "${{ matrix.os }}" == "windows-latest" ]; then
            7z a ../../balatro-seed-oracle-mcp-${{ matrix.runtime }}.zip *
          else
            tar -czf ../../balatro-seed-oracle-mcp-${{ matrix.runtime }}.tar.gz *
          fi

      - name: Upload Artifact
        uses: actions/upload-artifact@v4
        with:
          name: mcp-server-${{ matrix.runtime }}
          path: balatro-seed-oracle-mcp-${{ matrix.runtime }}.*

  release:
    needs: build
    runs-on: ubuntu-latest
    steps:
      - uses: actions/download-artifact@v4
      - uses: softprops/action-gh-release@v1
        with:
          files: balatro-seed-oracle-mcp-*
          generate_release_notes: true
```

## 🎉 Post-Publishing

1. **Test Installation** - Try installing from scratch on clean machine
2. **Monitor Issues** - Watch GitHub issues and Docker Hub comments
3. **Update Docs** - Fix any installation issues users report
4. **Version Bump** - Prepare for next release

---

**Result:** Your MCP server is now available to the world! 🌍

