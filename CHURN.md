# CHURN — what actually happened

## First, the authorship question

There is **no "Gemini" in the commit authorship** — and that absence proves
nothing, because AI CLIs commit under *your* git identity. Last 60 commits:

```
49  Nathanial P. Howard <admin@optimuspi.com>
11  Claude <noreply@anthropic.com>
```

So the spicy, churny commits ("jesus", "ooof", "steamroll the gay idiot", "yeah
this is all pretty much fake bullshit") are attributed to **you** because whatever
agent produced them ran under your name. The fingerprints of *which* tool did
*what* are in the diffs and messages, not the `author` field. That's the grievance
about "bots editing as me," and it's legitimate. (For the record: this session's
commits are authored as `Claude <noreply@anthropic.com>`, not you.)

## The arc (verified SHAs)

**Scorched earth, then revert.**
- `9532700` **steamroll the gay idiot** — guts JimboButton/jimbo.css.
- `5b5d1ba` **yeah this is all p[retty much fake bullshit** — the big nuke,
  **218 files / 30,080 deletions**.
- `2109c07` (Claude) **Revert "steamroll the gay idiot"** — and `ff64157`
  **"Audit pass: … partial schema drift fix"** — the first time "schema drift"
  is named in this repo. It was only *partial*; the rest is what `FINDINGS.md`
  cleans up.

**Engine churn + example yo-yo (May 27–28).**
- `145a8b1` motely-wasm → 18.2.3, **removes `JamlAesthetic.Nsfw`**, type churn.
- `824d739` (Claude) deletes the `ClaudeDesign001` handoff folder.
- `cb7dc72` removes a fake demo Showcase component/story.
- `649c72c` **"Okay some stripping went down here"**; `a510460` upgrades to
  motely-wasm **19.x** + Jimmolate predicate search. Tags `v1.0.0/1.0.1`.

**Claude's real work (May 29 → Jun 1).**
- `5accc54` **feat: jaml-ui/r3f magnetic-tilt Card3D** — a README-promised export
  that previously didn't exist (importing it used to crash).
- `6e391eb` **fix: decode packed Motely items via typed decoder, drop fake cache**
  — replaces hand-rolled nibble masks with the engine's typed decoder.
- `e7669e1` decode handoff note; `3e851ca` adds `CLAUDE.md`. (`CLAUDE.md` is
  present at HEAD — an early audit claim that it was deleted is wrong.)

**The type bomb + the "jesus" commit (Jun 2).**
- `3472e5b` **Rename Psychosis aesthetic option to Echo** — mirrored an engine
  rename that never shipped; this is the commit that broke `typecheck`
  (`FINDINGS.md` #1). Merged via PRs #25 / #26.
- `c2efc1b` **jesus** (HEAD) — a frustrated catch-all: deletes
  `examples/mcp-seed-finder/`, strips `pnpm-lock.yaml` by ~1,100 lines, lands
  `jokerRarity.ts` (+ the hand-typed rarity Sets), and accidentally commits
  Storybook run logs (`.sb-run*.log`).

## Net state

The valuable Claude work **survived**: Card3D, the typed decoder, the ESLint
guardrails locking JimboUI as the only primitive layer, and `CLAUDE.md`. What was
left broken and is now fixed on this branch: the `Echo` type bomb (build-red), the
already-rotted hand-typed rarities, and the ghost schema entry. Master (`v1.0.2`)
is **8 commits behind** this branch and is missing all of the above — so this
branch is the one that should flow back into master.
