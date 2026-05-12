# MotelyJAML — What Sucks (Honest Audit)

Snapshot: 2026-05-12, branch `feat/es-modules`.

Top issues in roughly descending pain order. File:line citations are best-effort; line numbers drift as you edit.

---

## HIGH severity

### 1. Single-thread worker path can deadlock
**`Motely/MotelySearch.cs:1205-1217`**
When `_threadCount == 1` the work runs synchronously on the calling thread. `_completionSource` won't resolve until `AwaitCompletion()` is called — if that's the same thread, you deadlock. Exceptions are caught locally but the task-like semantics are lost.

### 2. Multi-thread workers swallow exceptions
**`Motely/MotelySearch.cs:1224-1234`**
The thread lambda calls `RunWorkerBody()` with no try/catch. A worker throwing dies silently, `_completionSource` never sees it, and the caller hangs forever or thinks the search finished cleanly. Only the synchronous path wraps in try/catch — the threaded path needs the same.

### 3. Staged deletions of context docs
`AGENTS.md` and `HANDOFF.md` are staged for deletion. `HANDOFF.md` is the cross-session continuity doc. If you commit this as-is, the next Claude (or human) walks in cold. Either un-stage, or replace with a fresh CLAUDE.md before committing.

---

## MED severity

### 4. CLAUDE.md is empty
**`CLAUDE.md`**
File exists, has no content. By convention this is the codebase orientation doc — every fresh session re-ramps from scratch.

### 5. Magic constants in keyword counts
**`Motely/MotelySeedKeywordSequences.cs:68-70`**
`NsfwKeywordAestheticSeedCount`, `FunnyKeywordAestheticSeedCount`, `BalatroKeywordAestheticSeedCount` are baked numbers with no comment on how they were derived. If keyword lists drift again (as in `bd3a902a`), you'll silently desync — no automated check.

### 6. Dead code: `JokerSource` / `JokerSourceType`
**`Motely/Filters/Jaml/JamlConfig.cs:74-89`**
Defined, never used anywhere in the repo. Adjacent comment `// oops :( I have a complaint!` suggests abandoned mid-thought. Clutters public API surface.

### 7. Loose test deletion
**`Motely.Tests/JamlConfigTests.cs`**
`TryLoadFromPath_ResolvesTrimmedMixedCasePaths()` deleted as "bot-broken" but uncommitted, bundled with other changes. HANDOFF.md noted this needed a `git add -p` split — never finished.

---

## LOW severity

### 8. JAML typed clause lists are write-only
**`Motely/Filters/Jaml/JamlConfig.cs:12-41`**
After the polymorphism refactor (`67ff65dd`), `JamlSearchBuilder.CreatePlan()` only reads `OrderedClauses`. The 28 typed lists (`Jokers`, `CommonJokers`, `TarotCards`, …) are still populated by the loader and read by nothing. Pure waste — drop them next pass.

### 9. Duplicate JAML fixtures
**`Motely.Tests/GoldenJamlFiles/legendary-perkeo.jaml` ≡ `Zerkeo.jaml`**
Byte-identical. HANDOFF flagged this. Delete one.

### 10. Stale comment after PascalCase revert
**`Motely/MotelySearch.cs:1809`**
References old lowercase `tarot / planet / spectral`. Code was updated in `a9bf9aa9`, comment wasn't.

---

## What I'd tackle first

1. Fix the threading bugs (#1, #2) — these will bite you in production runs.
2. Decide on `HANDOFF.md` (#3) and write a real `CLAUDE.md` (#4) before committing this branch.
3. Everything else is housekeeping you can batch into one cleanup commit.
