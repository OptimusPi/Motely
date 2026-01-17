console.log('🔍 Debug seeding process...');

const EMBEDDINGS_WORKER = 'https://jaml-embeddings.optimuspi.workers.dev';

async function testEmbeddings() {
  console.log('Testing embeddings worker...');
  
  try {
    const response = await fetch(`${EMBEDDINGS_WORKER}/embed`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ text: 'Blueprint joker copy chain synergy' })
    });
    
    if (!response.ok) {
      console.log('❌ Embeddings worker error:', response.status, response.statusText);
      const text = await response.text();
      console.log('Response body:', text);
      return false;
    }
    
    const data = await response.json();
    console.log('✅ Embeddings working, got vector of length:', data.embedding?.length || 0);
    return data.embedding;
  } catch (error) {
    console.log('❌ Network error:', error.message);
    return false;
  }
}

async function testIndexing(embedding) {
  console.log('Testing indexing...');
  
  try {
    const response = await fetch(`${EMBEDDINGS_WORKER}/index`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({
        id: 'test-doc-1',
        embedding: embedding,
        metadata: { type: 'test', source: 'debug' }
      })
    });
    
    if (!response.ok) {
      console.log('❌ Indexing error:', response.status, response.statusText);
      const text = await response.text();
      console.log('Response body:', text);
      return false;
    }
    
    const data = await response.json();
    console.log('✅ Indexing working:', data);
    return true;
  } catch (error) {
    console.log('❌ Indexing network error:', error.message);
    return false;
  }
}

async function testQuerying() {
  console.log('Testing querying...');
  
  try {
    const response = await fetch(`${EMBEDDINGS_WORKER}/query`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({
        query: 'Blueprint joker copy chain',
        topK: 3
      })
    });
    
    if (!response.ok) {
      console.log('❌ Querying error:', response.status, response.statusText);
      const text = await response.text();
      console.log('Response body:', text);
      return false;
    }
    
    const data = await response.json();
    console.log('✅ Querying working, found results:', data.results?.length || 0);
    if (data.results && data.results.length > 0) {
      console.log('First result:', data.results[0]);
    }
    return true;
  } catch (error) {
    console.log('❌ Querying network error:', error.message);
    return false;
  }
}

async function main() {
  const embedding = await testEmbeddings();
  if (!embedding) {
    console.log('💥 Embeddings failed, stopping');
    return;
  }
  
  const indexed = await testIndexing(embedding);
  if (!indexed) {
    console.log('💥 Indexing failed, stopping');
    return;
  }
  
  await testQuerying();
  console.log('🎉 Debug complete!');
}

main().catch(console.error);
