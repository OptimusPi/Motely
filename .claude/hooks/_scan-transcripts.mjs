#!/usr/bin/env node
// One-shot transcript scanner: prints tool-call frequencies for Bash + MCP.
import { readFileSync, readdirSync, statSync } from 'node:fs';
import { join } from 'node:path';
import { homedir } from 'node:os';

const projectsDir = join(homedir(), '.claude', 'projects');
const files = [];
for (const proj of readdirSync(projectsDir)) {
  const dir = join(projectsDir, proj);
  if (!statSync(dir).isDirectory()) continue;
  for (const f of readdirSync(dir)) {
    if (!f.endsWith('.jsonl')) continue;
    const full = join(dir, f);
    files.push({ path: full, mtime: statSync(full).mtimeMs });
  }
}
files.sort((a, b) => b.mtime - a.mtime);
const recent = files.slice(0, 50);

const bashCounts = new Map();
const mcpCounts = new Map();

function extractBashKey(cmd) {
  if (typeof cmd !== 'string') return null;
  // Trim env-var prefixes like FOO=bar baz
  let s = cmd.replace(/^\s*(?:[A-Z_][A-Z0-9_]*=[^\s]*\s+)+/, '');
  // Strip leading wrappers
  s = s.replace(/^\s*(sudo|timeout\s+\d+\S*|nohup)\s+/, '');
  // First token before pipe / && / ; / |
  s = s.split(/\s*(?:\|\||&&|;|\|)\s*/)[0].trim();
  if (!s) return null;
  const tokens = s.split(/\s+/);
  if (tokens.length === 0) return null;
  // For commands like "git", "gh", "docker", "pnpm", "npm" — include subcommand
  const multi = new Set([
    'git', 'gh', 'docker', 'kubectl', 'pnpm', 'npm', 'yarn', 'bun',
    'cargo', 'go', 'pip', 'pipx', 'uv', 'poetry', 'rustup', 'aws', 'gcloud',
  ]);
  if (multi.has(tokens[0]) && tokens[1]) {
    return `${tokens[0]} ${tokens[1].replace(/^-+/, '').split('=')[0]}`;
  }
  return tokens[0];
}

for (const { path } of recent) {
  let raw;
  try { raw = readFileSync(path, 'utf8'); } catch { continue; }
  for (const line of raw.split(/\r?\n/)) {
    if (!line) continue;
    let obj;
    try { obj = JSON.parse(line); } catch { continue; }
    const content = obj?.message?.content;
    if (!Array.isArray(content)) continue;
    for (const item of content) {
      if (item?.type !== 'tool_use') continue;
      const name = item.name ?? '';
      if (name === 'Bash') {
        const key = extractBashKey(item.input?.command);
        if (key) bashCounts.set(key, (bashCounts.get(key) ?? 0) + 1);
      } else if (name.startsWith('mcp__')) {
        mcpCounts.set(name, (mcpCounts.get(name) ?? 0) + 1);
      }
    }
  }
}

const out = {
  scanned: recent.length,
  bash: [...bashCounts.entries()].sort((a, b) => b[1] - a[1]),
  mcp: [...mcpCounts.entries()].sort((a, b) => b[1] - a[1]),
};
console.log(JSON.stringify(out, null, 2));
