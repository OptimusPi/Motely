const fs = require('fs');
const path = require('path');

console.log('Testing file access...');

// Test JAML directory
const jamlDir = path.join(__dirname, '..', 'JamlFilters');
if (fs.existsSync(jamlDir)) {
  const files = fs.readdirSync(jamlDir).filter(f => f.endsWith('.jaml'));
  console.log(`✓ Found ${files.length} JAML files`);
  files.slice(0, 5).forEach(f => console.log(`  - ${f}`));
} else {
  console.log('❌ JamlFilters directory not found');
}

// Test Knowledge directory
const knowledgeDir = path.join(__dirname, '..', 'Motely.API', 'Knowledge');
if (fs.existsSync(knowledgeDir)) {
  const files = fs.readdirSync(knowledgeDir).filter(f => f.endsWith('.md'));
  console.log(`✓ Found ${files.length} knowledge files`);
  files.slice(0, 5).forEach(f => console.log(`  - ${f}`));
} else {
  console.log('❌ Knowledge directory not found');
}

// Test embeddings worker
console.log('Testing embeddings worker...');
fetch('https://jaml-embeddings.optimuspi.workers.dev/embed', {
  method: 'POST',
  headers: { 'Content-Type': 'application/json' },
  body: JSON.stringify({ text: 'test embedding' })
})
.then(response => response.json())
.then(data => {
  console.log('✓ Embeddings worker responding:', data.embedding ? 'has embedding data' : 'no embedding data');
})
.catch(error => {
  console.log('❌ Embeddings worker error:', error.message);
});
