# CLAUDE.md — MotelyJAML

Technical notes only. Personality: `$GROK_HOME/AGENTS.md`. Repo work-shape: `AGENTS.md`.
Handoff / Cowork / brainstorm markdown in this tree is **not** standing law unless the current message names it.

## Running things

Search:      dotnet run --project Motely.CLI -c Release -- --jaml JamlFilters/X.jaml
Score seeds: ... --source path/to/seeds.csv   (seeds ride the first column)
Publish:     dotnet publish -c Release Motely.CLI   (AOT; IL2104 is suppressed in the csproj)
Filters live in JamlFilters/. Validate JAML through the engine, not by eye.
`dotnet run -c Release` is the normal path. Don't reach for publish unless asked.

## Tests

The full Motely.Tests suite exceeds 64 GB of RAM and gets killed by Windows. Run tests
with `--filter` scoped to one class or method at a time. If a run gets killed, don't
re-run it unchanged.

## Working rules

- After any interrupt, re-read the last three user messages before choosing an action.
  Never re-propose the thing that was just interrupted.
- Before claiming a change works, re-run whatever proved the previous state worked.
  If it fails, roll back, then say so.
- Do not create memory or notes files on your own initiative.
