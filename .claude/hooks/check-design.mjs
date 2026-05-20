#!/usr/bin/env node
// PreToolUse hook: scans Edit/Write payloads for jaml-ui design rule violations.
// Blocks the tool call (exit 2) when a forbidden pattern is detected, with a
// human-readable reason that gets fed back to the model.
//
// Source of truth for rules: CLAUDE.md "Design rules" section.

import { readFileSync } from 'node:fs';

let raw = '';
try {
  raw = readFileSync(0, 'utf8');
} catch {
  process.exit(0);
}

let payload;
try {
  payload = JSON.parse(raw);
} catch {
  process.exit(0);
}

const tool = payload.tool_name ?? '';
const input = payload.tool_input ?? {};
if (tool !== 'Edit' && tool !== 'Write' && tool !== 'MultiEdit') process.exit(0);

const filePath = (input.file_path ?? '').replace(/\\/g, '/');
// Only enforce on TSX/JSX inside src/. Stories and src/ui/ get a pass for
// raw <button>/<input> (they are the primitives or test them directly).
if (!/\/src\//.test(filePath)) process.exit(0);
if (!/\.(tsx|jsx)$/.test(filePath)) process.exit(0);

const isUi = /\/src\/ui\//.test(filePath);
const isStory = /\.stories\.(tsx|jsx)$/.test(filePath);

// Collect every chunk of text the tool is about to write.
const chunks = [];
if (typeof input.content === 'string') chunks.push(input.content);
if (typeof input.new_string === 'string') chunks.push(input.new_string);
if (Array.isArray(input.edits)) {
  for (const e of input.edits) {
    if (typeof e?.new_string === 'string') chunks.push(e.new_string);
  }
}
if (chunks.length === 0) process.exit(0);
const text = chunks.join('\n');

const violations = [];

// 1. Raw <button>/<input>/<select>/<textarea> outside src/ui and stories.
if (!isUi && !isStory) {
  const m = /<(button|input|select|textarea)\b/.exec(text);
  if (m) {
    violations.push(
      `Raw <${m[1]}> detected. Use a Jimbo* primitive (JimboButton, etc.) from src/ui/. If the primitive is missing, add it to src/ui/ with a story.`,
    );
  }
}

// 2. Emoji in any UI/TSX file.
const EMOJI =
  /[\u{1F300}-\u{1FAFF}\u{2600}-\u{27BF}\u{1F000}-\u{1F2FF}\u{1F900}-\u{1F9FF}]/u;
if (EMOJI.test(text)) {
  violations.push(
    'Emoji detected in TSX. Use react-icons (react-icons/fi preferred) instead.',
  );
}

// 3. ALL CAPS shouting (single word 5+ chars, or multi-word caps phrase).
// Limit search to JSX text — string literals in code (like type names) are fine.
const JSX_TEXT_RE = />([^<>{}\n]*?)</g;
let jt;
const SHOUT_SINGLE = /\b[A-Z]{5,}\b/;
const SHOUT_MULTI = /\b[A-Z]{2,}\s+[A-Z]{2,}\b/;
while ((jt = JSX_TEXT_RE.exec(text)) !== null) {
  const t = jt[1].trim();
  if (!t) continue;
  const m = SHOUT_MULTI.exec(t) || SHOUT_SINGLE.exec(t);
  if (m) {
    violations.push(
      `ALL CAPS text "${m[0]}" detected. Jimbo design forbids shouting; use normal case.`,
    );
    break;
  }
}

// 4. fontWeight: bold / 700+ in inline JSX styles.
if (/fontWeight\s*:\s*(['"]?)(bold|bolder|[7-9]00)\1/.test(text)) {
  violations.push(
    'fontWeight bold/700+ detected in inline style. Jimbo design uses normal weight.',
  );
}

if (violations.length === 0) process.exit(0);

const reason = [
  'BLOCKED by jaml-ui design rules (.claude/hooks/check-design.mjs):',
  ...violations.map((v) => `  • ${v}`),
  '',
  'See CLAUDE.md "Design rules" and AGENTS.md. Fix before re-attempting the edit.',
].join('\n');

process.stderr.write(reason + '\n');
process.exit(2);
