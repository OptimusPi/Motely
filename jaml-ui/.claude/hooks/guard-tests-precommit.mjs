#!/usr/bin/env node
// Pre-commit guard: if .stories.tsx files are staged but no corresponding
// source files are staged, fail the commit. Catches the "edit the test to
// make it pass" anti-pattern at the commit boundary.
//
// Wire up with:   git config core.hooksPath .githooks
// (or call from .husky/pre-commit / .git/hooks/pre-commit directly)

import { execSync } from 'node:child_process';
import { existsSync } from 'node:fs';
import { basename, dirname, join } from 'node:path';

let staged = '';
try {
  staged = execSync('git diff --cached --name-only --diff-filter=ACMR', {
    encoding: 'utf8',
  });
} catch (e) {
  process.stderr.write('guard-tests-precommit: failed to read staged files\n');
  process.exit(0);
}

const files = staged.split(/\r?\n/).filter(Boolean);
const storyFiles = files.filter((f) => /\.stories\.(tsx|jsx|ts|js)$/.test(f));
if (storyFiles.length === 0) process.exit(0);

const sourceFiles = new Set(
  files.filter(
    (f) =>
      /\.(tsx|jsx|ts|js|mjs|cjs|css)$/.test(f) &&
      !/\.stories\.(tsx|jsx|ts|js)$/.test(f) &&
      !f.startsWith('.claude/'),
  ),
);

const orphans = [];
for (const story of storyFiles) {
  // Pair check: src/ui/foo.stories.tsx ↔ src/ui/foo.{tsx,ts}
  const base = basename(story).replace(/\.stories\.(tsx|jsx|ts|js)$/, '');
  const dir = dirname(story);
  const candidates = [
    join(dir, `${base}.tsx`),
    join(dir, `${base}.ts`),
    join(dir, `${base}.jsx`),
    join(dir, `${base}.js`),
  ].map((p) => p.replace(/\\/g, '/'));

  const sourceAlsoStaged = candidates.some((c) => sourceFiles.has(c));
  if (sourceAlsoStaged) continue;

  // If no source change is staged but the file exists, the story is being
  // edited in isolation — that's the cheat signature.
  const sourceExists = candidates.some((c) => existsSync(c));
  if (sourceExists) orphans.push({ story, expected: candidates.filter(existsSync) });
}

if (orphans.length === 0) process.exit(0);

process.stderr.write(
  [
    '',
    'COMMIT BLOCKED — story files edited without corresponding source changes:',
    '',
    ...orphans.map(
      (o) => `  • ${o.story}  (paired source: ${o.expected.join(', ')})`,
    ),
    '',
    'This catches the "edit the story to make it pass" anti-pattern.',
    'If you genuinely need to update the story alone (e.g. new variant, doc fix),',
    'bypass with:  git commit --no-verify',
    '',
  ].join('\n'),
);
process.exit(1);
