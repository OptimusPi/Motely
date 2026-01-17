const EMBEDDINGS_WORKER = 'https://jaml-embeddings.optimuspi.workers.dev';

async function indexDocument(id, text, metadata) {
  console.log(`📝 Indexing: ${id}`);
  
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
    console.log(`   ✅ Got embedding (${embedding.length} dimensions)`);
    
    // Index with embedding
    const indexResponse = await fetch(`${EMBEDDINGS_WORKER}/index`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ id, embedding, metadata })
    });
    
    if (!indexResponse.ok) {
      throw new Error(`Failed to index ${id}: ${await indexResponse.text()}`);
    }
    
    console.log(`   ✅ Indexed successfully`);
    return await indexResponse.json();
  } catch (error) {
    console.error(`   ❌ Failed: ${error.message}`);
    return null;
  }
}

async function main() {
  console.log('🚀 Seeding real JAML knowledge into Vectorize...');
  
  // Real JAML examples from your collection
  const jamlExamples = [
    {
      id: 'jaml-01WeeMonday',
      text: 'Erratic deck Wee Joker with Eternal sticker, wants 10+ Twos for high mult scaling. Early game filter focusing on Wee Joker synergy with playing card requirements.',
      metadata: { type: 'jaml-example', filename: '01WeeMonday.jaml', tags: 'erratic,wee,scaling' }
    },
    {
      id: 'jaml-meow_money',
      text: 'Lucky Money event filter, finds seeds with early lucky card money procs. Economy focused filter for Lucky Money events and financial jokers.',
      metadata: { type: 'jaml-example', filename: 'meow_money.jaml', tags: 'event,lucky,economy' }
    },
    {
      id: 'jaml-blueprint_brainstorm',
      text: 'Blueprint and Brainstorm jokers for infinite copy chain synergy. Must have both jokers early for powerful scaling combos.',
      metadata: { type: 'jaml-example', filename: 'blueprint_brainstorm.jaml', tags: 'synergy,copy,blueprint,brainstorm' }
    },
    {
      id: 'jaml-hanging_chad',
      text: 'Hanging Chad joker filter, works well with Photograph and other face jokers. Focus on early game Hanging Chad availability.',
      metadata: { type: 'jaml-example', filename: 'hanging_chad.jaml', tags: 'joker,face,synergy' }
    },
    {
      id: 'jaml-economy_build',
      text: 'Early economy build with Golden Joker, Business Card, Reserved Parking. Focus on money generation in antes 1-3.',
      metadata: { type: 'jaml-example', filename: 'economy_build.jaml', tags: 'economy,money,early' }
    }
  ];
  
  // Game knowledge
  const gameKnowledge = [
    {
      id: 'knowledge-blueprint-synergy',
      text: 'Blueprint copies the joker to its right. Best synergies: Brainstorm, Showman, Baron. Often paired for infinite copy chains. Position matters - Blueprint must be left of target joker.',
      metadata: { type: 'game-knowledge', source: 'game-mechanics.md', category: 'synergy' }
    },
    {
      id: 'knowledge-economy-early',
      text: 'Early economy strategies: Reserved Parking, Golden Joker, Credit Card, Business Card. LuckyMoney events proc at 1/15 chance. Focus on antes 1-3 for money generation.',
      metadata: { type: 'game-knowledge', source: 'strategy-patterns.md', category: 'economy' }
    },
    {
      id: 'knowledge-erratic-deck',
      text: 'Erratic deck starts with random jokers. Wee Joker benefits from specific playing card counts. Eternal stickers keep jokers across runs. Good for high scaling builds.',
      metadata: { type: 'game-knowledge', source: 'deck-strategies.md', category: 'deck' }
    },
    {
      id: 'knowledge-copy-chains',
      text: 'Copy chain mechanics: Blueprint copies rightmost joker, Brainstorm copies all jokers, Showman copies leftmost joker. Chain them for exponential scaling. Position and order critical.',
      metadata: { type: 'game-knowledge', source: 'advanced-mechanics.md', category: 'synergy' }
    },
    {
      id: 'knowledge-edition-value',
      text: 'Card editions: Foil (+50 chips), Holographic (+10 mult), Polychrome (+1.5 mult), Negative (free, +1 mult). Negative Perkeo is top tier for infinite scaling.',
      metadata: { type: 'game-knowledge', source: 'edition-guide.md', category: 'mechanics' }
    }
  ];
  
  // Prompt examples (few-shot learning)
  const promptExamples = [
    {
      id: 'example-blueprint-request',
      text: 'User asked: "blueprint brainstorm copy chain". Generated JAML with Blueprint ante 1, Brainstorm ante 1-2, Showman as optional. Result was successful copy chain build.',
      metadata: { type: 'prompt-example', prompt: 'blueprint brainstorm copy chain', category: 'synergy' }
    },
    {
      id: 'example-economy-request',
      text: 'User asked: "early economy with lucky money". Generated JAML with Lucky Money event, Golden Joker, Business Card. Focused on antes 1-3 for money generation.',
      metadata: { type: 'prompt-example', prompt: 'early economy with lucky money', category: 'economy' }
    },
    {
      id: 'example-erratic-wee',
      text: 'User asked: "erratic deck wee joker scaling". Generated JAML with Erratic deck, Wee Joker, Eternal sticker, target 10+ Twos. High mult scaling build.',
      metadata: { type: 'prompt-example', prompt: 'erratic deck wee joker scaling', category: 'deck' }
    }
  ];
  
  console.log(`\n📚 Seeding ${jamlExamples.length} JAML examples...`);
  for (const example of jamlExamples) {
    await indexDocument(example.id, example.text, example.metadata);
  }
  
  console.log(`\n🧠 Seeding ${gameKnowledge.length} knowledge entries...`);
  for (const knowledge of gameKnowledge) {
    await indexDocument(knowledge.id, knowledge.text, knowledge.metadata);
  }
  
  console.log(`\n💭 Seeding ${promptExamples.length} prompt examples...`);
  for (const example of promptExamples) {
    await indexDocument(example.id, example.text, example.metadata);
  }
  
  console.log('\n🎉 Seeding complete! Vectorize now has JAML knowledge base.');
  console.log('Test with: curl -X POST https://jaml-embeddings.optimuspi.workers.dev/query -d \'{"query":"blueprint copy chain","topK":3}\'');
}

main().catch(console.error);
