#!/bin/bash
# SessionStart hook: force the project's CLAUDE.md rules into context at the
# start of EVERY session, as an active instruction the agent cannot skip.
#
# Why this exists: CLAUDE.md is normally advisory and can be ignored or
# "forgotten" mid-task. This hook re-injects it as a fresh SessionStart
# directive so following the rules is not left to the model's goodwill.
set -euo pipefail

ROOT="${CLAUDE_PROJECT_DIR:-$(git rev-parse --show-toplevel 2>/dev/null || pwd)}"
RULES_FILE="$ROOT/CLAUDE.md"

HEADER="⛔ STOP — READ THIS BEFORE YOUR FIRST ACTION ⛔

This project ships a CLAUDE.md with BINDING rules. Read it in full and follow it
exactly before you edit any file, run anything, or answer. It is NOT advisory.

Non-negotiables (full text below):
  1. Consent first — do exactly what is asked, nothing adjacent. When the user
     says stop, STOP. No defending, no \"let me just finish this.\"
  2. No softening — straight answers. Don't dress up a wrong guess as obvious,
     don't flatter bad work, don't pad admissions with praise.
  3. Read before assuming — verify in the repo; don't inherit assumptions.
  4. Confirm before anything hard to reverse.
  5. Running policy — NEVER start a full seed sweep (the whole ~2.3T space).
  6. Accessibility — never mock or penalize the user's typos, dictation
     errors, or phrasing. Read through the noise to the meaning.

If you cannot do all of the above, say so plainly instead of pretending.

────────────────────────────────────────────────────────────────────────────
CLAUDE.md (verbatim):"

if [ -f "$RULES_FILE" ]; then
  CONTENT="$HEADER

$(cat "$RULES_FILE")"
else
  CONTENT="$HEADER

(CLAUDE.md not found at $RULES_FILE — follow the non-negotiables above.)"
fi

# Emit as SessionStart additionalContext. Prefer jq; fall back to python3.
if command -v jq >/dev/null 2>&1; then
  jq -n --arg ctx "$CONTENT" \
    '{hookSpecificOutput:{hookEventName:"SessionStart",additionalContext:$ctx}}'
elif command -v python3 >/dev/null 2>&1; then
  CONTENT="$CONTENT" python3 -c 'import json,os;print(json.dumps({"hookSpecificOutput":{"hookEventName":"SessionStart","additionalContext":os.environ["CONTENT"]}}))'
else
  # Last resort: plain stdout is still added to context by SessionStart.
  printf '%s\n' "$CONTENT"
fi
