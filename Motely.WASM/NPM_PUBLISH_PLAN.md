# NPM Publish Plan: Motely.WASM Cleanup & GitHub Actions

## Overview

This plan covers cleaning up the Motely.WASM package and setting up automated npm publishing via GitHub Actions.

---

## Phase 1: Pre-Publish Cleanup ✅

### 1.1 Verify Package Structure

**Current State:**
- ✅ `package.json` configured correctly
- ✅ `dist/` in `.gitignore` (build artifacts excluded)
- ✅ `poc/` excluded from npm package (via `files` array)
- ✅ TypeScript definitions (`motely-wasm.d.ts`) included
- ✅ Build scripts configured (`build:wasm`, `copy:bundle`)

**Action Items:**
- [x] Verify `.gitignore` excludes `dist/`, `bin/`, `obj/`, `node_modules/`
- [x] Verify `package.json` `files` array excludes `poc/`
- [ ] **TODO**: Add `poc/` to `.gitignore`? (Decision: Keep POC in repo for testing, but exclude from npm)

### 1.2 Clean Build Artifacts

**Before Publishing:**
```bash
cd external/Motely/Motely.WASM
# Remove any stale build artifacts
rm -rf dist/ bin/ obj/ node_modules/
```

**Note**: GitHub Actions will do a clean build, so this is mainly for local testing.

### 1.3 Verify Build Process

**Test Build Locally:**
```bash
cd external/Motely/Motely.WASM
npm install
npm run build
# Verify dist/app-bundle/ contains:
#   - main.js
#   - _framework/ (with all .wasm files)
#   - Motely.WASM.runtimeconfig.json
```

**Expected Output:**
- `dist/app-bundle/main.js` exists
- `dist/app-bundle/_framework/` contains all WASM files
- No errors during build

---

## Phase 2: GitHub Actions Workflow ✅

### 2.1 Current Workflow Status

**File**: `.github/workflows/publish-motely-wasm.yml`

**Current Implementation:**
- ✅ Triggers on tag `motely-wasm-v*` or manual dispatch
- ✅ Checks out repo with submodules
- ✅ Sets up .NET and Node.js
- ✅ Extracts version from tag or manual input
- ✅ Builds and publishes to npm

**Status**: **READY** - Workflow is correctly configured

### 2.2 Workflow Improvements Needed

**Current Issues:**
1. ✅ Version extraction works correctly
2. ✅ Build process is correct
3. ⚠️ **TODO**: Add build verification step before publish
4. ⚠️ **TODO**: Add npm dry-run test before actual publish
5. ⚠️ **TODO**: Add changelog/version notes (optional)

**Recommended Additions:**

```yaml
# Add before npm publish:
- name: Verify build output
  run: |
    if [ ! -f dist/app-bundle/main.js ]; then
      echo "ERROR: main.js not found in dist/app-bundle/"
      exit 1
    fi
    if [ ! -d dist/app-bundle/_framework ]; then
      echo "ERROR: _framework/ not found in dist/app-bundle/"
      exit 1
    fi
    echo "Build verification passed"

- name: Test npm package (dry-run)
  run: npm pack --dry-run
```

---

## Phase 3: NPM Token Setup ✅

### 3.1 Prerequisites

**Required:**
- [ ] npm account created at https://www.npmjs.com
- [ ] Automation token created (Settings → Access Tokens → Generate New Token)
- [ ] Token added to GitHub Secrets as `NPM_TOKEN`

**Current Status**: 
- ⚠️ **TODO**: Verify `NPM_TOKEN` secret exists in GitHub repo settings

### 3.2 Token Type

**Recommended**: Automation token (scoped, no expiration)
- Better security than Classic tokens
- Designed for CI/CD
- Can be scoped to specific packages

---

## Phase 4: Publishing Process

### 4.1 Publishing via Tag (Recommended)

**Steps:**
```bash
# 1. Ensure all changes are committed
git add .
git commit -m "Prepare Motely.WASM v1.0.0"

# 2. Create and push tag
git tag motely-wasm-v1.0.0
git push origin motely-wasm-v1.0.0
```

**What Happens:**
- GitHub Actions triggers on tag push
- Extracts version `1.0.0` from tag
- Updates `package.json` version
- Builds WASM bundle
- Publishes to npm

### 4.2 Publishing via Manual Workflow

**Steps:**
1. Go to GitHub → Actions → "Publish Motely WASM to npm"
2. Click "Run workflow"
3. Optionally enter version override (e.g., `1.0.1`)
4. Click "Run workflow"

**What Happens:**
- Uses version from `package.json` (or override)
- Builds and publishes

---

## Phase 5: Post-Publish Verification

### 5.1 Verify Package on npm

**Check:**
- Visit https://www.npmjs.com/package/motely-wasm
- Verify version is published
- Verify package size is reasonable
- Verify `dist/app-bundle/` is included

### 5.2 Test Installation

**Test in Clean Environment:**
```bash
# Create test directory
mkdir test-motely-wasm
cd test-motely-wasm

# Install package
npm install motely-wasm

# Verify files
ls node_modules/motely-wasm/dist/app-bundle/
# Should see: main.js, _framework/, etc.

# Test copy script
npx motely-wasm-copy-to-public
# Should copy to public/motely-wasm/
```

---

## Phase 6: Cleanup Tasks

### 6.1 Repository Cleanup

**Before First Publish:**
- [ ] Remove any test/development files from `poc/` that shouldn't be in repo
- [ ] Ensure `README.md` is up-to-date
- [ ] Verify `NPM_PUBLISH.md` instructions are accurate
- [ ] Add `.npmignore` if needed (or rely on `files` array)

### 6.2 Documentation Updates

**Update:**
- [ ] `README.md` - Add npm installation instructions
- [ ] `NPM_PUBLISH.md` - Verify all steps are current
- [ ] Add CHANGELOG.md (optional but recommended)

---

## Checklist: Ready to Publish

### Pre-Publish Checklist

- [ ] **POC tested and working** ✅ (Ready for testing)
- [ ] **Build process verified** (Test `npm run build` locally)
- [ ] **Package.json version set** (e.g., `1.0.0`)
- [ ] **NPM_TOKEN secret configured** in GitHub
- [ ] **Workflow file reviewed** (`.github/workflows/publish-motely-wasm.yml`)
- [ ] **README.md updated** with npm instructions
- [ ] **No sensitive data** in package files
- [ ] **.gitignore** excludes build artifacts

### Publish Checklist

- [ ] **Tag created** (`motely-wasm-v1.0.0`) OR **Manual workflow triggered**
- [ ] **GitHub Actions run** successfully
- [ ] **Package appears on npm** (https://www.npmjs.com/package/motely-wasm)
- [ ] **Installation tested** in clean environment
- [ ] **Copy script works** (`npx motely-wasm-copy-to-public`)

---

## Recommended Workflow Improvements

### Add to `.github/workflows/publish-motely-wasm.yml`:

```yaml
- name: Verify build output
  working-directory: external/Motely/Motely.WASM
  run: |
    if [ ! -f dist/app-bundle/main.js ]; then
      echo "::error::main.js not found in dist/app-bundle/"
      exit 1
    fi
    if [ ! -d dist/app-bundle/_framework ]; then
      echo "::error::_framework/ directory not found"
      exit 1
    fi
    echo "✅ Build verification passed"
    ls -lh dist/app-bundle/_framework/*.wasm | wc -l
    echo "WASM files found"

- name: Test npm package structure
  working-directory: external/Motely/Motely.WASM
  run: |
    npm pack --dry-run
    echo "✅ Package structure verified"
```

---

## Summary

**POC Status**: ✅ **READY FOR TESTING**

**NPM Publish Status**: ✅ **READY** (after POC testing)

**Next Steps:**
1. Test POC locally (`cd poc && npm install && npm start`)
2. Verify POC works end-to-end
3. Add workflow improvements (build verification)
4. Set up NPM_TOKEN secret (if not already done)
5. Publish via tag: `git tag motely-wasm-v1.0.0 && git push origin motely-wasm-v1.0.0`
