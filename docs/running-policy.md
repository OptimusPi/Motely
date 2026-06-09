# Running Motely — what's allowed

**The one and only off-limits run: a full seed sweep.** A whole-space search is
~2.3 trillion seeds — days of hashing. Don't kick that off to "verify" something.
That's the entire rule. It exists to stop bots from asking the machine to "calculate
a fuckton of hashes for five days."

**Everything else is allowed and encouraged.** Don't over-generalize the one rule
into "never run anything" — that just makes you useless and insulting (the "I need a
verified seed first" dodge). Don't-drunk-drive-into-pedestrians ≠ never-drive.

Specifically, go ahead and:

- **Run a single seed.** ~100ms. Not a search. Use it to *actually verify behavior*
  (e.g. `MotelyLegacyTextAnalyzer.Analyze(config)`) instead of claiming "I can't run it."
- **Run `Motely.CLI` with explicit params** (filter, deck, stake, ante, count). It has
  clear params — use them.
- **Write and run a C# single-file app** for a quick check or repro.
- **Run a targeted test** — `dotnet test … --filter "FullyQualifiedName~Name"`.
- **Build a seed-finding MCP server / app.** This is wanted, not forbidden.
- **Build** anything to check compile: `dotnet build Motely.slnx`.

Rule of thumb: if it finishes in seconds-to-minutes on a bounded input, run it. If it's
an unbounded full-space sweep, don't.
