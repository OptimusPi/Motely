const { execSync } = require('child_process');
const path = require('path');

// Change to scripts directory
process.chdir(__dirname);

try {
  console.log('Starting Vectorize seeding...');
  execSync('node --loader ts-node/esm seed-vectorize.ts', { stdio: 'inherit', timeout: 60000 });
  console.log('✅ Seeding complete!');
} catch (error) {
  console.error('❌ Seeding failed:', error.message);
  process.exit(1);
}
