const fs = require('fs');
const path = require('path');

const EMBEDDINGS_WORKER = 'https://jaml-embeddings.optimuspi.workers.dev';

async function indexDocument(id, text, metadata) {
  console.log(`Indexing: ${id}`);
  
  try {
    // Get embedding
    const embedResponse = await fetch(`${EMBEDDINGS_WORKER}/embed`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ text })
    });
    
    if (!embedResponse.ok) {
      throw new Error(`Failed to embed ${id}: ${await embedResponse.text()}`);
    }
    
    const { embedding } = await embedResponse.json();
    
    // Index with embedding
    const indexResponse = await fetch(`${EMBEDDINGS_WORKER}/index`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ id, embedding, metadata })
    });
    
    if (!indexResponse.ok) {
      throw new Error(`Failed to index ${id}: ${await indexResponse.text()}`);
    }
    
    console.log(`✓ Indexed: ${id}`);
    return await indexResponse.json();
  } catch (error) {
    console.error(`❌ Failed to index ${id}:`, error.message);
  }
}

async function main() {
  console.log('Starting simple seeding...');
  
  // Index a few JAML examples manually
  const examples = [
    {
      id: 'jaml-blueprint-brainstorm',
      text: 'JAML filter with Blueprint and Brainstorm jokers for copy chain synergy, must have both jokers early game',
      metadata: { type: 'jaml-example', filename: 'blueprint-brainstorm.jaml', jaml: 'name: Blueprint Brainstorm Copy Chain\ndeck: Red\nstake: White\nmust:\n  - joker: Blueprint\n    antes: [1, 2]\n  - joker: Brainstorm\n    antes: [1, 2]\nshould: []\nmustNot: []' }
    },
    {
      id: 'jaml-lucky-money',
      text: 'JAML filter for Lucky Money event, finds seeds with early lucky card money procs and economy jokers',
      metadata: { type: 'jaml-example', filename: 'lucky-money.jaml', jaml: 'name: Lucky Money Economy\ndeck: Red\nstake: White\nmust:\n  - event: Lucky\nshould:\n  - joker: GoldenTicket\n    score: 1\n  - joker: BusinessCard\n    score: 1\nmustNot: []' }
    },
    {
      id: 'knowledge-blueprint-synergy',
      text: 'Blueprint copies the joker to its right. Best synergies: Brainstorm, Showman, Baron. Often paired for infinite copy chains.',
      metadata: { type: 'game-knowledge', source: 'game-mechanics.md' }
    },
    {
      id: 'knowledge-economy-early',
      text: 'Early economy strategies: Reserved Parking, Golden Joker, Credit Card. LuckyMoney events proc at 1/15 chance.',
      metadata: { type: 'game-knowledge', source: 'strategy-patterns.md' }
    }
  ];
  
  for (const example of examples) {
    await indexDocument(example.id, example.text, example.metadata);
  }
  
  console.log('✅ Simple seeding complete!');
}

main().catch(console.error);
