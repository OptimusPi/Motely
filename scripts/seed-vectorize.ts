import fs from 'fs';
import path from 'path';

const EMBEDDINGS_WORKER = 'https://jaml-embeddings.optimuspi.workers.dev';

// Function to describe JAML in natural language
function describeJaml(jaml: string, filename: string): string {
  const lines = jaml.split('\n');
  let description = `JAML filter "${filename}": `;
  
  const mustItems: string[] = [];
  const shouldItems: string[] = [];
  const mustNotItems: string[] = [];
  
  let currentSection: string[] = [];
  let sectionType = '';
  
  for (const line of lines) {
    const trimmed = line.trim();
    if (trimmed.startsWith('must:')) {
      sectionType = 'must';
      currentSection = [];
    } else if (trimmed.startsWith('should:')) {
      sectionType = 'should';
      currentSection = [];
    } else if (trimmed.startsWith('mustNot:')) {
      sectionType = 'mustNot';
      currentSection = [];
    } else if (trimmed.startsWith('- ') && sectionType) {
      const item = trimmed.substring(2);
      if (item.includes('joker:')) {
        const jokerName = item.split('joker:')[1].trim();
        if (sectionType === 'must') mustItems.push(jokerName);
        else if (sectionType === 'should') shouldItems.push(jokerName);
        else if (sectionType === 'mustNot') mustNotItems.push(jokerName);
      }
    }
  }
  
  if (mustItems.length > 0) {
    description += `Must have: ${mustItems.join(', ')}`;
  }
  if (shouldItems.length > 0) {
    description += `. Should have: ${shouldItems.join(', ')}`;
  }
  if (mustNotItems.length > 0) {
    description += `. Must not have: ${mustNotItems.join(', ')}`;
  }
  
  return description;
}

// Function to chunk text into smaller pieces
function chunkMarkdown(text: string, chunkSize: number): string[] {
  const chunks: string[] = [];
  const sentences = text.split('. ');
  let currentChunk = '';
  
  for (const sentence of sentences) {
    if (currentChunk.length + sentence.length > chunkSize && currentChunk) {
      chunks.push(currentChunk.trim());
      currentChunk = sentence;
    } else {
      currentChunk += (currentChunk ? '. ' : '') + sentence;
    }
  }
  
  if (currentChunk) {
    chunks.push(currentChunk.trim());
  }
  
  return chunks;
}

async function indexDocument(id: string, text: string, metadata: Record<string, string>) {
  console.log(`Indexing: ${id}`);
  
  // Get embedding first
  const embedResponse = await fetch(`${EMBEDDINGS_WORKER}/embed`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ text })
  });
  
  if (!embedResponse.ok) {
    throw new Error(`Failed to embed ${id}: ${await embedResponse.text()}`);
  }
  
  const { embedding } = await embedResponse.json() as { embedding: number[] };
  
  // Then index with embedding
  const indexResponse = await fetch(`${EMBEDDINGS_WORKER}/index`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ id, embedding, metadata })
  });
  
  if (!indexResponse.ok) {
    throw new Error(`Failed to index ${id}: ${await indexResponse.text()}`);
  }
  
  return await indexResponse.json();
}

async function main() {
  console.log('Starting Vectorize seeding...');
  
  try {
    // 1. Index JAML examples
    const jamlDir = './JamlFilters';
    if (fs.existsSync(jamlDir)) {
      const jamlFiles = fs.readdirSync(jamlDir).filter(f => f.endsWith('.jaml'));
      
      for (const file of jamlFiles) {
        const jaml = fs.readFileSync(path.join(jamlDir, file), 'utf-8');
        const name = file.replace('.jaml', '');
        const description = describeJaml(jaml, name);
        
        await indexDocument(`jaml-${name}`, description, {
          type: 'jaml-example',
          filename: file,
          jaml: jaml
        });
        
        console.log(`✓ Indexed JAML: ${file}`);
      }
    }
    
    // 2. Index game knowledge chunks
    const knowledgeDir = './Motely.API/Knowledge';
    if (fs.existsSync(knowledgeDir)) {
      const knowledgeFiles = fs.readdirSync(knowledgeDir).filter(f => f.endsWith('.md'));
      
      for (const file of knowledgeFiles) {
        const content = fs.readFileSync(path.join(knowledgeDir, file), 'utf-8');
        const chunks = chunkMarkdown(content, 500);
        
        for (let i = 0; i < chunks.length; i++) {
          await indexDocument(`knowledge-${file}-${i}`, chunks[i], {
            type: 'game-knowledge',
            source: file,
            chunk: i.toString()
          });
        }
        
        console.log(`✓ Indexed knowledge: ${file} (${chunks.length} chunks)`);
      }
    }
    
    console.log('✅ Seeding complete!');
    
  } catch (error) {
    console.error('❌ Seeding failed:', error);
    process.exit(1);
  }
}

main();
