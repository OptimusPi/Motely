#!/usr/bin/env node
// One-shot audit: find unused CSS custom properties and unused .j-* classes.
import { readFileSync, readdirSync, statSync } from 'node:fs';
import { join, extname } from 'node:path';

// Tokens live in jimbo-tokens.css; component classes in jimbo.css.
const TOKENS_CSS = readFileSync('src/ui/jimbo-tokens.css', 'utf8');
const CSS = readFileSync('src/ui/jimbo.css', 'utf8');
const ALL_CSS = `${TOKENS_CSS}\n${CSS}`;

// Extract token names defined on :root (and in @media variants).
const tokenSet = new Set();
for (const m of ALL_CSS.matchAll(/(--j[a-zA-Z0-9-]+)\s*:/g)) tokenSet.add(m[1]);

// Extract class names defined in jimbo.css (+ any utilities).
const classSet = new Set();
for (const m of ALL_CSS.matchAll(/\.(j-[a-zA-Z0-9_-]+)/g)) classSet.add(m[1]);

// Walk source tree and collect references.
function walk(dir, out = []) {
  for (const f of readdirSync(dir)) {
    const full = join(dir, f);
    const st = statSync(full);
    if (st.isDirectory()) { if (!/node_modules|dist|storybook-static/.test(full)) walk(full, out); }
    else if (/\.(ts|tsx|js|jsx|mjs|css|html|json)$/.test(f)) out.push(full);
  }
  return out;
}
const files = walk('src');
files.push('src/ui/jimbo.css', 'src/ui/jimbo-tokens.css');
const SOURCES = files.map((f) => readFileSync(f, 'utf8')).join('\n');

const unusedTokens = [];
for (const t of tokenSet) {
  // Each token is "used" if referenced via var(--j-foo) somewhere outside its own definition line.
  const re = new RegExp(`var\\(\\s*${t}[,\\s\\)]`, 'g');
  const hits = (SOURCES.match(re) || []).length;
  if (hits === 0) unusedTokens.push(t);
}

const unusedClasses = [];
for (const c of classSet) {
  // Class is used if referenced as "j-foo" anywhere (in className strings, CSS selectors don't count).
  // We exclude self-references in jimbo.css itself.
  const re = new RegExp(`\\b${c}\\b`, 'g');
  const hits = (SOURCES.match(re) || []).length;
  // Each declaration in jimbo.css counts at least once. Find how many declarations exist.
  const declRe = new RegExp(`\\.${c}\\b`, 'g');
  const decls = (CSS.match(declRe) || []).length;
  if (hits <= decls) unusedClasses.push(c);
}

console.log(JSON.stringify({
  tokenCount: tokenSet.size,
  classCount: classSet.size,
  unusedTokens: unusedTokens.sort(),
  unusedClasses: unusedClasses.sort(),
}, null, 2));
