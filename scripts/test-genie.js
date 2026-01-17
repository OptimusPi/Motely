console.log('🧞 Testing JamlGenie with RAG...');

fetch('https://jamlgenie.optimuspi.workers.dev', {
  method: 'POST',
  headers: { 'Content-Type': 'application/json' },
  body: JSON.stringify({
    prompt: 'blueprint copy chain synergy'
  })
})
.then(response => response.json())
.then(data => {
  console.log('✅ JamlGenie Response:');
  console.log(JSON.stringify(data, null, 2));
})
.catch(error => {
  console.log('❌ JamlGenie Error:', error.message);
});
