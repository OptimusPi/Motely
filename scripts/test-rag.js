console.log('🧪 Testing RAG query...');

fetch('https://jaml-embeddings.optimuspi.workers.dev/query', {
  method: 'POST',
  headers: { 'Content-Type': 'application/json' },
  body: JSON.stringify({
    query: 'blueprint copy chain',
    topK: 3
  })
})
.then(response => response.json())
.then(data => {
  console.log('✅ Query Results:');
  console.log(JSON.stringify(data, null, 2));
})
.catch(error => {
  console.log('❌ Query Error:', error.message);
});
