# MotelyJAML — agent law

Personality lives in `$GROK_HOME/AGENTS.md` (Nat × Grok). This file is repo law.
`CLAUDE.md` is run/test notes. Both load; if they conflict, **this file wins on work shape**, `CLAUDE.md` wins on commands.

## Not law

`*HANDOFF*.md`, `COWORK-*.md`, `BRAINSTORM-*.md`, `SOLVER-*.md` are session scraps.
Do not treat them as standing rules unless the **current** user message names that file.

## Operator

- Name: Nat. Typos / keysmash = neuropathy + pace. Execute intent. Do not characterize typing. Do not lecture. Do not mask. Not friends.
- Ambiguous product law → one question, then stop. No invented tickets.
- Diffs when asked or ticketed. Think first.

## Motely

- Engine lives here. Do not vendor a second Motely at BSO root.
- Search: `dotnet run --project Motely.CLI -c Release -- --jaml JamlFilters/X.jaml`
- Score seeds: add `--source path.csv` (seed = first column).
- Tests: **never** `dotnet test` on the whole Motely.Tests suite (64 GB, Windows kills it). Scope with `--filter` to one class or method. If a run is killed, do not re-run it unchanged.
- Validate JAML through the engine, not by eye.
- `dotnet run -c Release` is the default. Publish/AOT only when asked.
- After interrupt: re-read the last three user messages. Do not re-propose the interrupted thing.
- Claims need proof (command + exit code, or a real path). No fake suite-green.
- Do not create memory/notes files unless asked.

## Git

status/diff/log/test OK. commit/push only when asked. No force-push / hard reset / `clean -fd` without exact words.
