import { copyFileSync, mkdirSync, writeFileSync, readdirSync, statSync } from 'fs';
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
  const jamlOutDir = join(__dirname, '../Motely.API/wwwroot/JAML');
  mkdirSync(jamlOutDir, { recursive: true });

  const schemaSource = join(__dirname, '../jaml.schema.json');
  const schemaTarget = join(jamlOutDir, 'jaml.schema.json');
  copyFileSync(schemaSource, schemaTarget);

  console.log('Schema copied successfully');

  // Copy index.html to API wwwroot for root endpoint
  const apiIndexSource = join(__dirname, '../public/index.html');
  const apiIndexTarget = join(__dirname, '../Motely.API/wwwroot/index.html');
  mkdirSync(join(__dirname, '../Motely.API/wwwroot'), { recursive: true });
  copyFileSync(apiIndexSource, apiIndexTarget);
  
  console.log('API index.html copied successfully');
  
  // Copy JamlGenie static files from public/ to wwwroot/
  const jamlGenieSourceDir = join(__dirname, '../public/JamlGenie');
  const jamlGenieTargetDir = join(__dirname, '../Motely.API/wwwroot/JamlGenie');
  
  try {
    if (statSync(jamlGenieSourceDir).isDirectory()) {
      mkdirSync(jamlGenieTargetDir, { recursive: true });
      const files = readdirSync(jamlGenieSourceDir);
      for (const file of files) {
        const sourcePath = join(jamlGenieSourceDir, file);
        const targetPath = join(jamlGenieTargetDir, file);
        if (statSync(sourcePath).isFile()) {
          copyFileSync(sourcePath, targetPath);
        }
      }
      console.log('JamlGenie files copied successfully');
    }
  } catch (error) {
    console.warn('JamlGenie source directory not found, skipping copy:', error.message);
  }
} catch (error) {
  console.error('Failed to copy font:', error);
  process.exit(1);
}
