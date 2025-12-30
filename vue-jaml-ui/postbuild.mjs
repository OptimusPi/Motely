import { copyFileSync, mkdirSync } from 'fs';
import { join, dirname } from 'path';
import { fileURLToPath } from 'url';

const __dirname = dirname(fileURLToPath(import.meta.url));

try {
  // Ensure fonts directory exists
  const fontsDir = join(__dirname, '../wwwroot/fonts');
  mkdirSync(fontsDir, { recursive: true });

  // Copy font file
  const source = join(__dirname, '../public/m6x11plus.ttf');
  const target = join(fontsDir, 'm6x11plus.ttf');
  copyFileSync(source, target);

  console.log('Font copied successfully');

  // Copy JAML schema into the deployed site output (served under /JAML/)
  const jamlOutDir = join(__dirname, '../wwwroot/JAML');
  mkdirSync(jamlOutDir, { recursive: true });

  const schemaSource = join(__dirname, '../jaml.schema.json');
  const schemaTarget = join(jamlOutDir, 'jaml.schema.json');
  copyFileSync(schemaSource, schemaTarget);

  console.log('Schema copied successfully');
} catch (error) {
  console.error('Failed to copy font:', error);
  process.exit(1);
}
