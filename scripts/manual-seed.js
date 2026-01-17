// Manual seeding with explicit console output
const EMBEDDINGS_WORKER = 'https://jaml-embeddings.optimuspi.workers.dev';

console.log('🚀 Starting manual seeding...');

// Test 1: Simple embedding
async function test1() {
  console.log('\n=== Test 1: Embedding ===');
  
  try {
    const response = await fetch(`${EMBEDDINGS_WORKER}/embed`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ text: 'Blueprint joker copy chain' })
    });
    
    console.log('Status:', response.status);
    const result = await response.text();
    console.log('Response:', result.substring(0, 200) + '...');
    
    return response.ok;
  } catch (e) {
    console.log('Error:', e.message);
    return false;
  }
}

// Test 2: Index a document
async function test2() {
  console.log('\n=== Test 2: Indexing ===');
  
  try {
    const response = await fetch(`${EMBEDDINGS_WORKER}/index`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({
        id: 'test-blueprint',
        embedding: new Array(768).fill(0.1), // Dummy embedding
        metadata: { type: 'jaml-example', filename: 'test.jaml' }
      })
    });
    
    console.log('Status:', response.status);
    const result = await response.text();
    console.log('Response:', result);
    
    return response.ok;
  } catch (e) {
    console.log('Error:', e.message);
    return false;
  }
}

// Test 3: Query
async function test3() {
  console.log('\n=== Test 3: Querying ===');
  
  try {
    const response = await fetch(`${EMBEDDINGS_WORKER}/query`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({
        query: 'Blueprint joker',
        topK: 3
      })
    });
    
    console.log('Status:', response.status);
    const result = await response.text();
    console.log('Response:', result);
    
    return response.ok;
  } catch (e) {
    console.log('Error:', e.message);
    return false;
  }
}

async function run() {
  const t1 = await test1();
  const t2 = await test2();
  const t3 = await test3();
  
  console.log('\n=== Results ===');
  console.log('Embedding:', t1 ? '✅' : '❌');
  console.log('Indexing:', t2 ? '✅' : '❌');
  console.log('Querying:', t3 ? '✅' : '❌');
  
  if (t1 && t2 && t3) {
    console.log('🎉 All tests passed! Ready to seed real data.');
  } else {
    console.log('💥 Some tests failed. Check the errors above.');
  }
}

run();
