import { promises as fs } from 'fs';
import { join, dirname } from 'path';
import { fileURLToPath } from 'url';

const __dir = dirname(fileURLToPath(import.meta.url));

async function main() {
  const goldenDir = join(__dir, '..', '..', '..', 'Motely.Tests', 'GoldenJamlFiles');
  const snippetsFile = join(__dir, '..', 'snippets', 'jaml.code-snippets');

  const files = await fs.readdir(goldenDir);
  let snippets = {
    "Basic JAML filter": {
      "prefix": "jaml-basic",
      "body": [
        "deck: ${1:Red}",
        "stake: ${2:White}",
        "must:",
        "  - joker: ${3:Blueprint}",
        "    antes: [${4:1}]"
      ],
      "description": "Create a basic JAML filter."
    },
    "Scored JAML criterion": {
      "prefix": "jaml-should",
      "body": [
        "should:",
        "  - joker: ${1:Brainstorm}",
        "    score: ${2:50}",
        "    antes: [${3:1, 2, 3}]"
      ],
      "description": "Create a scored JAML criterion."
    },
    "Legendary joker criterion": {
      "prefix": "jaml-legendary",
      "body": [
        "must:",
        "  - legendaryJoker: ${1:Perkeo}",
        "    antes: [${2:1}]",
        "    sources:",
        "      arcanaPacks: true",
        "      spectralPacks: true"
      ],
      "description": "Create a legendary joker criterion."
    }
  };

  for (const file of files) {
    if (!file.endsWith('.jaml')) continue;
    const outName = file.replace('.jaml', '');
    const content = await fs.readFile(join(goldenDir, file), 'utf8');
    snippets[`Golden: ${outName}`] = {
      prefix: `jaml-${outName}`,
      body: content.split(/\r?\n/),
      description: `Test boilerplate for ${file}`
    };
  }

  await fs.writeFile(snippetsFile, JSON.stringify(snippets, null, 2), 'utf8');
  console.log(`Generated ${Object.keys(snippets).length} snippets.`);
}

main().catch(console.error);