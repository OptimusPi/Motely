/** Read top-level `seeds:` entries from a JAML document. */
export function parseJamlSeeds(jaml: string): string[] {
  const lines = jaml.split(/\r?\n/);
  const seeds: string[] = [];
  let inSeeds = false;

  for (const line of lines) {
    if (/^seeds:\s*$/.test(line)) {
      inSeeds = true;
      continue;
    }
    if (!inSeeds) continue;

    const item = line.match(/^\s+-\s+(\S+)\s*$/);
    if (item) {
      seeds.push(item[1]);
      continue;
    }
    if (line.trim() === "") continue;
    break;
  }

  return seeds;
}

function formatSeedsBlock(seeds: string[]): string[] {
  if (seeds.length === 0) return ["seeds:"];
  return ["seeds:", ...seeds.map((seed) => `  - ${seed}`)];
}

/**
 * Write seeds into JAML the same way Motely CLI `--save-seeds` does:
 * replace the top-level `seeds:` block (or append one if missing).
 */
export function mergeSeedsIntoJaml(jaml: string, seeds: string[], max = 1000): string {
  const capped = [...new Set(seeds.map((s) => s.trim()).filter(Boolean))].slice(0, max);
  const block = formatSeedsBlock(capped);
  const lines = jaml.split(/\r?\n/);
  const seedsLineIdx = lines.findIndex((line) => /^seeds:\s*$/.test(line));

  if (seedsLineIdx >= 0) {
    let end = seedsLineIdx + 1;
    while (end < lines.length) {
      const line = lines[end];
      if (/^\s+-\s+\S+/.test(line)) {
        end += 1;
        continue;
      }
      if (line.trim() === "") {
        end += 1;
        continue;
      }
      break;
    }

    const before = lines.slice(0, seedsLineIdx);
    while (before.length > 0 && before[before.length - 1].trim() === "") {
      before.pop();
    }
    const after = lines.slice(end);
    return [...before, ...block, ...after].join("\n").replace(/\n+$/, "") + "\n";
  }

  const trimmed = jaml.replace(/\n+$/, "");
  return `${trimmed}\n${block.join("\n")}\n`;
}
