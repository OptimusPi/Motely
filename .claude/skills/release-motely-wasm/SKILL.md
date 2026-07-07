---
name: release-motely-wasm
description: Ship a real motely-wasm release to npm — version bump, green suites, publish, tag. The one true release ritual.
disable-model-invocation: true
---

# Release motely-wasm

Run this straight through, no check-ins, until the one real stop point at the end.

1. Bump `<MotelyVersion>` in `Directory.Packages.props`. The `SyncNpmPackageVersion` build target stamps `Motely.Wasm/package.json` from it automatically — don't hand-edit the package.json.

2. Prove it green, in order, from the repo root then `Motely.Wasm/`:
   ```sh
   dotnet test
   npm test          # publishes Release into dist/, runs the Node suite against dist/index.mjs
   npm run test:ui   # Playwright in real Chromium against the same artifact
   ```
   Any red: stop and fix, don't ship past a failure.

3. Commit, tag with the bare version (`git tag <version>`, matching `<MotelyVersion>` exactly), push the commit.

4. `npm run pack:check` — eyeball tarball contents/version.

5. **Stop here.** `npm publish` writes to the public registry — this is the one step that needs an explicit go, same as any irreversible action affecting shared state for any user, not special handling. Everything above (tests, tag, commit) is reversible local work — do all of it without pausing.

6. On go: `npm publish`, then `git push --tags`.
