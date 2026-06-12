#!/usr/bin/env bash
# Stop hook — refuse to "finish" while the current change contains soft code.
#
# Diff-scoped: scans only ADDED lines (git diff vs HEAD) plus untracked files, so
# pre-existing markers never trip it and brand-new unstaged files aren't a blind
# spot. Pure git + grep, no dotnet, never runs a search. Blocks via exit 2 +
# stderr (Claude Code feeds stderr back to the model on a Stop hook).
set -uo pipefail

ROOT="${CLAUDE_PROJECT_DIR:-$(git rev-parse --show-toplevel 2>/dev/null)}"
[ -n "$ROOT" ] || exit 0
cd "$ROOT" 2>/dev/null || exit 0
git rev-parse --is-inside-work-tree >/dev/null 2>&1 || exit 0

# Soft-code patterns (case-insensitive). C#-aware: NotImplementedException stubs.
SOFT='(\.\.\. *existing code|rest of (the )?(code|function|file|implementation)|(your code|implementation|code) (goes )?here|code goes here|omitted for brevity|abbreviated for brevity|similar to above|see (implementation )?above|continue (from|implementation) here|TODO:? *implement|NotImplementedException|throw new [A-Za-z]*Exception\( *["'"'"'][^"'"'"']*not[ _-]?implemented)'
TAGGED='(TODO|FIXME)\((#?[A-Za-z0-9_-]+)\)'   # allowed: TODO(#12), TODO(JIRA-3)
BARE='(TODO|FIXME)'
IGNORE='^\.claude/'

hits=""
record() { # path lineno content
  local path="$1" lineno="$2" c="$3" why=""
  [[ "$path" =~ $IGNORE ]] && return
  if echo "$c" | grep -qiE "$SOFT"; then
    why="placeholder / stub"
  elif echo "$c" | grep -qE "$BARE" && ! echo "$c" | grep -qE "$TAGGED"; then
    why="bare TODO/FIXME — tag it: TODO(#123)"
  fi
  [ -n "$why" ] && hits+="  - ${path}:${lineno}  [${why}]  $(echo "$c" | sed 's/^[[:space:]]*//' | cut -c1-90)"$'\n'
}

# 1. added lines in tracked diff
file=""; lineno=0
while IFS= read -r line; do
  case "$line" in
    '+++ b/'*) file="${line#+++ b/}";;
    '@@'*) hunk="${line#*+}"; lineno="${hunk%%[, ]*}";;
    '+'*) record "$file" "$lineno" "${line:1}"; lineno=$((lineno+1));;
    '-'*) ;;
    *) ;;
  esac
done < <(git diff HEAD --unified=0 -- '*.cs' '*.csproj' '*.json' '*.jaml' 2>/dev/null)

# 2. untracked (brand-new) files, scanned whole
while IFS= read -r f; do
  [ -n "$f" ] || continue
  case "$f" in *.cs|*.csproj|*.json|*.jaml) ;; *) continue;; esac
  n=0
  while IFS= read -r c || [ -n "$c" ]; do n=$((n+1)); record "$f" "$n" "$c"; done < "$f"
done < <(git ls-files --others --exclude-standard 2>/dev/null)

if [ -n "$hits" ]; then
  {
    echo "Soft code in this change — resolve before finishing:"
    printf '%s' "$hits"
    echo
    echo "Replace placeholders/stubs with real implementation."
    echo "Deferrals must be tagged: TODO(#issue)."
  } >&2
  exit 2
fi
exit 0
