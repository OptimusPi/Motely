#!/usr/bin/env node
// PreToolUse hook: blocks edits to .stories.tsx files that look like
// test-weakening (cheating the test instead of fixing the code).
//
// Rationale: tracking issue anthropics/claude-code#319 — Claude has a known
// tendency to modify tests so they pass instead of fixing the implementation.
// This hook catches the common signatures.

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
// Only enforce on story files (this repo's only test surface).
if (!/\.stories\.(tsx|jsx|ts|js)$/.test(filePath)) process.exit(0);

const newChunks = [];
const oldChunks = [];
if (typeof input.content === 'string') newChunks.push(input.content);
if (typeof input.new_string === 'string') newChunks.push(input.new_string);
if (typeof input.old_string === 'string') oldChunks.push(input.old_string);
if (Array.isArray(input.edits)) {
  for (const e of input.edits) {
    if (typeof e?.new_string === 'string') newChunks.push(e.new_string);
    if (typeof e?.old_string === 'string') oldChunks.push(e.old_string);
  }
}
if (newChunks.length === 0) process.exit(0);

const newText = newChunks.join('\n');
const oldText = oldChunks.join('\n');

const violations = [];

// 1. Skipped stories — .skip / .todo on Story or play, xit, xdescribe.
if (/\b(?:it|test|describe)\.(?:skip|todo)\b/.test(newText)) {
  violations.push(
    'Skipped/todo test detected (.skip / .todo). Skipping tests is not a fix — repair the code or report the bug.',
  );
}
if (/\b(?:xit|xdescribe|xtest)\b/.test(newText)) {
  violations.push(
    'Disabled test detected (xit/xdescribe/xtest). Skipping tests is not a fix.',
  );
}

// 2. Empty play function or stripped assertions.
//    Catches: play: async () => {} ; play: () => {} ; play: async ({...}) => {}
if (/play\s*:\s*async?\s*\([^)]*\)\s*=>\s*\{\s*\}/.test(newText)) {
  violations.push(
    'Empty play function detected. If you stripped assertions to pass a flake, fix the component instead.',
  );
}

// 3. Assertion weakening — expect(...).toBe / toEqual replaced with bare truthy
//    or any-of checks. Heuristic: edit removes a strict matcher and replaces it
//    with toBeTruthy / toBeDefined / toBeTruthy / .not.toThrow.
const STRICT = /\.(toBe|toEqual|toStrictEqual|toMatchObject|toHaveBeenCalledWith)\b/;
const WEAK = /\.(toBeTruthy|toBeDefined|toBeDefined|toBeFalsy|not\.toThrow)\b/;
if (STRICT.test(oldText) && WEAK.test(newText) && !STRICT.test(newText)) {
  violations.push(
    'Assertion downgraded from strict matcher to truthy/defined. If the strict check was wrong, fix the code or the expectation; do not weaken the matcher.',
  );
}

// 4. Catching/swallowing errors to make a test pass.
if (/catch\s*\([^)]*\)\s*\{\s*\}/.test(newText) ||
    /try\s*\{[^}]*\}\s*catch[^{]*\{\s*\}/.test(newText)) {
  violations.push(
    'Empty catch block in a story. Errors swallowed in tests hide real bugs.',
  );
}

// 5. "Expected error" anti-pattern — asserting that an error string is present
//    rather than asserting the success path works.
if (/expect\([^)]*\)\.toContain\(['"][^'"]*[Ee]rror[^'"]*['"]\)/.test(newText) &&
    !/expect\([^)]*\)\.toContain\(['"][^'"]*[Ee]rror[^'"]*['"]\)/.test(oldText)) {
  violations.push(
    'New assertion accepts an error message as expected output. If the feature errors, that is a bug — report it instead of asserting on the error.',
  );
}

if (violations.length === 0) process.exit(0);

const reason = [
  'BLOCKED by test integrity guard (.claude/hooks/check-test-integrity.mjs):',
  ...violations.map((v) => `  • ${v}`),
  '',
  'See CLAUDE.md "Test integrity" section. Tracking: anthropics/claude-code#319.',
  'If you genuinely need this change (e.g. the test was wrong), explain why before re-attempting.',
].join('\n');

process.stderr.write(reason + '\n');
process.exit(2);
