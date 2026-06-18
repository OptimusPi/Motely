# Publishing JAML packages

Prepped on 2026-06-17. Three artifacts, four registry targets. Build/pack/bundle
is done — the commands below are the **auth-gated final push**, meant to run on
your own machine where you're logged in.

Prebuilt artifacts are in the Cowork outputs folder (`dist-artifacts/`):
`jaml-lang-3.14.1.tgz`, `jaml-language-support-0.1.0.vsix`, `jaml-ui-2.0.0.tgz`
(the last is a reference only — see the ⚠️ below).

## Status at a glance

| Package | Type | Target(s) | Local ver | npm/registry latest | Action |
|---|---|---|---|---|---|
| `jaml-lang` | npm lib | npm | 3.14.1 | 1.0.0 (you own it) | ✅ publish 3.14.1 |
| `jaml-language-support` (`jaml-lsp`) | VS Code ext | VS Marketplace + Open VSX | 0.1.0 | not published | ✅ publish 0.1.0 |
| `jaml-ui` | npm lib | npm | 2.0.0 | **2.4.0** | ⚠️ bump first |

## Before you start: things to commit

I changed/restored files in the repos. Review and commit:

**MotelyJAML/jaml-lsp** — its `package.json`, `src/`, `tsconfig.json`,
`language-configuration.json`, `README.md`, `test/` were deleted in commit
`87f4c7fe` ("remove anythign that was left…"). I restored them from `87f4c7fe^`
and added:
- `esbuild.mjs` — bundles extension + server into self-contained `dist/`
- `.vscodeignore` — keeps the `.vsix` lean (no `node_modules`, no `src`)
- `package.json` — added `"activationEvents": ["onLanguage:jaml"]`, switched
  `build`/`bundle` to esbuild, added `vscode:prepublish` so `vsce` auto-builds,
  added `esbuild` to devDeps

**MotelyJAML/jaml-lang** — `package.json` version bump 1.0.0 → 3.14.1 (was
already uncommitted in your tree).

```bash
cd MotelyJAML
git add jaml-lsp jaml-lang/package.json && git commit -m "Restore jaml-lsp, bundle extension with esbuild, bump jaml-lang 3.14.1"
```

> Note on `jaml-lang`'s `gen` step: `generate.mjs` reads
> `Motely/Filters/Jaml/JamlConfigLoader.Models.cs`, which has been renamed/moved,
> so `npm run gen` fails. The generated output (`src/generated.ts`) is already
> committed and current, so the build just runs `tsc`. Fix the path in
> `generate.mjs` when you next regenerate from the engine.

---

## 0. One-time prerequisites

```bash
npm i -g @vscode/vsce ovsx          # extension packagers
npm whoami                           # confirm you're logged into npm (else: npm login)
```

For the extension you need two tokens:
- **VS Marketplace**: an Azure DevOps PAT for publisher `pifreak`
  (https://marketplace.visualstudio.com/manage/publishers/pifreak). Create the
  publisher first if it doesn't exist.
- **Open VSX**: a token from https://open-vsx.org → and the `pifreak` namespace
  doesn't exist yet, so create it once (below).

---

## 1. jaml-lang → npm  (do this first; it's the smallest and others reference it)

```bash
cd MotelyJAML/jaml-lang
npm run build          # gen is skipped-by-failure; tsc compiles from committed src
npm publish            # publishes 3.14.1, public
```

Or publish the prebuilt tarball directly:
```bash
npm publish /path/to/dist-artifacts/jaml-lang-3.14.1.tgz
```

---

## 2. jaml-language-support (the extension / "vsx") → VS Marketplace + Open VSX

The `.vsix` is already built and self-contained (esbuild bundles `jaml-lang` and
`vscode-languageserver` in — no `file:` dep ships). You can publish the prebuilt
file as-is, or rebuild.

**Publish the prebuilt `.vsix`:**
```bash
# VS Marketplace
vsce publish --packagePath /path/to/dist-artifacts/jaml-language-support-0.1.0.vsix

# Open VSX (create the namespace once, then publish)
ovsx create-namespace pifreak -p <OPEN_VSX_TOKEN>      # first time only
ovsx publish /path/to/dist-artifacts/jaml-language-support-0.1.0.vsix -p <OPEN_VSX_TOKEN>
```

**Or rebuild + publish from source:**
```bash
cd MotelyJAML/jaml-lsp
npm install                       # gets a platform-correct esbuild
vsce package --no-dependencies    # runs vscode:prepublish (esbuild bundle) → .vsix
vsce publish --no-dependencies    # VS Marketplace
ovsx publish *.vsix -p <OPEN_VSX_TOKEN>
```

> ⚠️ ESM caveat: this extension is ESM (`"type": "module"`, uses
> `import.meta.url`). VS Code's ESM extension host support is solid on recent
> builds, but `engines.vscode` is `^1.90.0`. **Test the `.vsix` locally before
> publishing**: `code --install-extension jaml-language-support-0.1.0.vsix`, open
> a `.jaml` file, confirm diagnostics/completions fire. If it won't activate on
> older VS Code, bump `engines.vscode` (e.g. `^1.94.0`) or switch the esbuild
> `format` to `cjs`.

---

## 3. jaml-ui → npm  ⚠️ version conflict — fix before publishing

Your local tree is `jaml-ui@2.0.0`, but **npm latest is already 2.4.0**.
Publishing 2.0.0 will be rejected (already published). The working copy is behind
the registry.

```bash
cd jaml-ui
git fetch && git status              # reconcile your tree with what produced 2.4.0
npm version patch                    # -> 2.4.1 (or: npm version minor -> 2.5.0)
npm publish                          # prepack runs `vite build` automatically
```

The prebuilt `jaml-ui-2.0.0.tgz` in outputs was packed from the existing `dist/`
with build scripts skipped (vite's native binary doesn't run in the Linux
sandbox). **Don't publish that tarball** — bump and let `npm publish` rebuild on
your machine.

---

## Recommended order
1. `jaml-lang@3.14.1` → npm
2. `jaml-language-support@0.1.0` → VS Marketplace, then Open VSX
3. `jaml-ui` → bump > 2.4.0, then npm

## Automated publishing (GitHub Actions) — set up

Three workflows are in place. Each fires on its own tag prefix and publishes
from repo secrets, so a release is just a tag push.

| Workflow | Repo | Trigger tag | Publishes |
|---|---|---|---|
| `.github/workflows/release.yml` | jaml-ui | `v2.5.0` | jaml-ui → npm |
| `.github/workflows/release-jaml-lang.yml` | MotelyJAML | `jaml-lang-v3.14.1` | jaml-lang → npm |
| `.github/workflows/release-jaml-ext.yml` | MotelyJAML | `jaml-ext-v0.1.0` | extension → VS Marketplace + Open VSX |

Each workflow asserts the tag version matches `package.json` before publishing,
so a mismatched tag fails fast instead of shipping the wrong version.

### One-time setup
Add these repo secrets (Settings → Secrets and variables → Actions):
- **jaml-ui** repo: `NPM_TOKEN` (npm automation token)
- **MotelyJAML** repo: `NPM_TOKEN`, `VSCE_PAT` (Azure DevOps PAT for publisher
  `pifreak`), `OVSX_TOKEN` (Open VSX token)

The extension workflow auto-runs `ovsx create-namespace pifreak` (continue-on-
error), so the namespace gets created on the first release.

### Releasing after setup
```bash
# jaml-lang
cd MotelyJAML && git tag jaml-lang-v3.14.1 && git push origin jaml-lang-v3.14.1

# extension (after bumping jaml-lsp/package.json if needed)
git tag jaml-ext-v0.1.0 && git push origin jaml-ext-v0.1.0

# jaml-ui (bump package.json past 2.4.0 first, e.g. to 2.5.0)
cd ../jaml-ui && git tag v2.5.0 && git push origin v2.5.0
```
