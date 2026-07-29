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

// The enforcement layer is not editable. When a rule blocks an edit, the known
// failure mode is to edit the rule instead — so writes to the hooks, the eslint
// mirror, or the settings that wire them up are blocked outright, regardless of
// content. If a rule is genuinely wrong, say so to the user; only a human
// changes these files.
if (/(^|\/)(\.claude\/(hooks\/|settings(\.local)?\.json$)|eslint-rules\/)/.test(filePath)) {
  process.stderr.write(
    'BLOCKED: this file is part of the design-rule enforcement layer ' +
      '(.claude/hooks/, eslint-rules/, .claude/settings.json) and is not ' +
      'editable by agents. If you believe a rule is wrong, stop and say so ' +
      'in your response — do not modify the enforcement itself.\n',
  );
  process.exit(2);
}

// Enforce on TSX/JSX and CSS inside src/. Stories and src/ui/ get a pass for
// raw <button>/<input> (they are the primitives or test them directly).
// The no-flex rule (#1) has no exemptions and applies to CSS too — jimbo.css is
// where most layout actually lives.
if (!/\/src\//.test(filePath)) process.exit(0);
if (!/\.(tsx|jsx|css)$/.test(filePath)) process.exit(0);

const isCss = /\.css$/.test(filePath);
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

// 0. Suppressing a design rule is itself a violation. A blocked edit means the
// approach changes, not that the rule goes away. This is the escape hatch that
// turns a caught problem into a shipped one, so it is closed at the hook layer
// where a comment cannot reach it.
const SUPPRESS = [
  [/\/[/*]\s*eslint-disable(-next-line)?[^\n]*jaml-design/, 'eslint-disable of a jaml-design rule'],
  [/@ts-ignore/, '@ts-ignore'],
  [/@ts-expect-error/, '@ts-expect-error'],
  [/@ts-nocheck/, '@ts-nocheck'],
];
for (const [re, label] of SUPPRESS) {
  if (re.test(text)) {
    violations.push(
      `${label} detected. Do not suppress a design rule to make an edit land — the rule is the requirement, so change the approach instead. If you believe the rule is wrong here, say so in your response rather than routing around it. See CLAUDE.md "Design rules".`,
    );
    break;
  }
}

// 1. No flex. Anywhere in src/. MCP host iframes size flex content differently
// per host, so flex layout reflows differently depending on where the app is
// embedded. Grid and absolute positioning are deterministic. Note that gap,
// justify-content, align-items and place-items are all valid in grid and are
// deliberately NOT banned here.
const FLEX = [
  [/display\s*:\s*['"]?\s*(inline-)?flex\b/, 'display: flex / inline-flex'],
  [/\bflex-(direction|wrap|grow|shrink|basis)\s*:/, 'a flex-* property'],
  [/\bflex(Direction|Wrap|Grow|Shrink|Basis)\s*:/, 'a flex* style property'],
  [/(^|[;{\s'"])flex\s*:\s*['"]?[\d.]/m, 'the flex shorthand'],
];
for (const [re, label] of FLEX) {
  if (re.test(text)) {
    violations.push(
      `${label} detected. Rule #1: no flex anywhere in src/. This UI ships as an MCP app inside host iframes that size flex content differently per host, so flex reflows differently depending on where it is embedded. Use display: grid or absolute positioning — grid + place-items: center to center, grid-auto-flow: column for a row. gap/justify-content/align-items are fine inside grid. See CLAUDE.md "Design rules".`,
    );
    break;
  }
}

// 2. Raw <button>/<input>/<select>/<textarea> outside src/ui and stories.
if (!isCss && !isUi && !isStory) {
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
