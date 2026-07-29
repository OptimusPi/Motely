# AGENTS.md

The design rules and build constraints for this repo live in **[CLAUDE.md](./CLAUDE.md)**.

Read the "Design rules" section there before writing any UI code. It is the source of
truth cited by both enforcement layers:

- `.claude/hooks/check-design.mjs` — a `PreToolUse` hook that blocks the write (exit 2)
- `eslint-rules/jaml-design.js` — a CI backstop

Rule #1 is **no flex, anywhere in `src/`** — this UI ships as an MCP app inside host
iframes that size flex content differently per host, so layout must be grid or absolute
to render identically everywhere. The full reasoning is in CLAUDE.md.

Do not disable a rule to make an edit go through. See CLAUDE.md "Design rules".
