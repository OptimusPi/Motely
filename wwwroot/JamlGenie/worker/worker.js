// Cloudflare Workers AI for JamlGenie
// Deploys automatically via GitHub Actions

export default {
  async fetch(request, env) {
    // Handle CORS preflight
    if (request.method === 'OPTIONS') {
      return new Response(null, {
        headers: {
          'Access-Control-Allow-Origin': '*',
          'Access-Control-Allow-Methods': 'POST, OPTIONS',
          'Access-Control-Allow-Headers': 'Content-Type',
          'Access-Control-Max-Age': '86400',
        },
      });
    }

    // Only allow POST
    if (request.method !== 'POST') {
      return jsonResponse({ success: false, error: 'Method not allowed' }, 405);
    }

    try {
      const { prompt } = await request.json();

      if (!prompt || typeof prompt !== 'string' || prompt.trim().length === 0) {
        return jsonResponse({ success: false, error: 'Prompt required' }, 400);
      }

      // Use Cloudflare Workers AI - @cf/meta/llama-3.1-8b-instruct
      const aiResponse = await env.AI.run('@cf/meta/llama-3.1-8b-instruct', {
        messages: [
          {
            role: 'system',
            content: `You are a Balatro filter generator. Convert natural language into MotelyJsonConfig format (the JSON config format used by MotelyJAML).

Return ONLY valid JSON matching MotelyJsonConfig schema, no markdown, no explanation, no code blocks.

TOP-LEVEL STRUCTURE:
{
  "name": "Filter Name",
  "description": "Optional",
  "author": "Optional",
  "dateCreated": "2025-12-21T00:00:00.000Z",
  "deck": "Red",
  "stake": "White",
  "mode": "sum",  // Optional: "sum" (default) or "max"
  "must": [],
  "should": [],
  "mustNot": []
}

CLAUSE STRUCTURE (for must/should/mustNot arrays):
{
  "type": "Joker|SoulJoker|Voucher|TarotCard|SpectralCard|PlanetCard|BossBlind|Tag|SmallBlindTag|BigBlindTag|PlayingCard|And|Or",
  "value": "ItemName",  // Single value OR use "values": ["Item1", "Item2"] for multi-value (OR matching)
  "antes": [1, 2, 3, 4],  // Array of ante numbers (0-39), defaults to all if omitted
  "edition": "Negative|Polychrome|Foil|Holographic|None",  // Optional
  "score": 10,  // Optional, for should clauses
  "min": 1,  // Optional, minimum count required
  "stickers": ["Eternal", "Perishable", "Rental"],  // Optional array
  "sources": {  // Optional - for shop/pack slot targeting
    "shopSlots": [0, 1, 2, 3],
    "packSlots": [0, 1],
    "minShopSlot": 0,
    "maxShopSlot": 10,
    "minPackSlot": 0,
    "maxPackSlot": 3,
    "tags": true,
    "requireMega": false
  },
  "clauses": [  // For And/Or operators
    { /* nested clause */ }
  ],
  "suit": "Club|Diamond|Heart|Spade",  // For PlayingCard
  "rank": "Two|Three|Four|Five|Six|Seven|Eight|Nine|Ten|Jack|Queen|King|Ace",  // For PlayingCard
  "seal": "Red|Blue|Gold|Purple",  // For PlayingCard
  "enhancement": "Bonus|Mult|Wild|Glass|Steel|Stone|Lucky"  // For PlayingCard
}

VALID TYPES (case-sensitive):
- "Joker" - Regular jokers (Blueprint, Brainstorm, etc.)
- "SoulJoker" - Soul jokers (Perkeo, Triboulet, Canio, Chicot, etc.)
- "Voucher" - Vouchers (Telescope, Observatory, Hieroglyph, etc.)
- "TarotCard" - Tarot cards (TheFool, TheMagician, etc.)
- "SpectralCard" - Spectral cards (Ankh, Soul, Wraith, etc.)
- "PlanetCard" - Planet cards (Jupiter, Mars, Venus, etc.)
- "BossBlind" - Boss blinds (TheGoad, CeruleanBell, TheOx, etc.)
- "Tag" - Generic tag (matches both small and big blind)
- "SmallBlindTag" - Small blind tags only
- "BigBlindTag" - Big blind tags only
- "PlayingCard" - Playing cards (use suit/rank, not value)
- "And" - All nested clauses must match
- "Or" - Any nested clause can match

VALID DECKS: Red, Blue, Yellow, Green, Black, Ghost, Checkered, Zodiac
VALID STAKES: White, Yellow, Orange, Green, Blue, Black, Purple, Red, Gold
VALID EDITIONS: Negative, Polychrome, Foil, Holographic, None
VALID MODES: "sum" (default), "max" (or "max_count", "maxcount")

IMPORTANT RULES:
- Use "value" for single item, "values" array for multiple items (OR matching)
- Cannot use both "value" and "values" in same clause
- Antes array: 0-39 (ante 0 = before first boss, ante 1 = first boss, etc.)
- If antes omitted, defaults to all antes [1,2,3,4,5,6,7,8]
- Soul jokers NEVER appear in shops - only packs
- Sources.shopSlots and Sources.packSlots are arrays of slot indices
- For PlayingCard, use "suit" and "rank" properties, NOT "value"

EXAMPLE INPUT: "I want 2 blueprints"
EXAMPLE OUTPUT:
{
  "name": "2 Blueprints",
  "deck": "Red",
  "stake": "White",
  "must": [
    {"type": "Joker", "value": "Blueprint", "antes": [1, 2, 3, 4, 5, 6, 7, 8]},
    {"type": "Joker", "value": "Blueprint", "antes": [1, 2, 3, 4, 5, 6, 7, 8]}
  ],
  "should": [],
  "mustNot": []
}

Always include: name, deck, stake, must (array), should (array), mustNot (array)`
          },
          {
            role: 'user',
            content: prompt.trim()
          }
        ],
        max_tokens: 1500
      });

      // Extract response text
      const responseText = aiResponse.response || aiResponse || '';
      
      // Parse JSON from response
      let config;
      try {
        // Remove markdown code blocks if present
        let cleaned = responseText.trim();
        cleaned = cleaned.replace(/```json\n?/g, '').replace(/```\n?/g, '').trim();
        
        // Try to find JSON object
        const jsonMatch = cleaned.match(/\{[\s\S]*\}/);
        if (jsonMatch) {
          config = JSON.parse(jsonMatch[0]);
        } else {
          config = JSON.parse(cleaned);
        }
      } catch (parseError) {
        console.error('Parse error:', parseError, 'Response:', responseText);
        return jsonResponse({
          success: false,
          error: 'Failed to parse AI response as JSON. Try rephrasing your request.'
        }, 500);
      }

      // Ensure required structure matches MotelyJsonConfig EXACTLY
      if (!config.must) config.must = [];
      if (!config.should) config.should = [];
      if (!config.mustNot) config.mustNot = [];
      if (!config.name) config.name = 'Generated Filter';
      if (!config.deck) config.deck = 'Red';
      if (!config.stake) config.stake = 'White';
      
      // Normalize type capitalization to match MotelyJsonConfig expectations
      const normalizeType = (type) => {
        if (!type) return type;
        const lower = type.toLowerCase();
        const typeMap = {
          'joker': 'Joker',
          'souljoker': 'SoulJoker',
          'voucher': 'Voucher',
          'tarot': 'TarotCard',
          'tarotcard': 'TarotCard',
          'spectral': 'SpectralCard',
          'spectralcard': 'SpectralCard',
          'planet': 'PlanetCard',
          'planetcard': 'PlanetCard',
          'boss': 'BossBlind',
          'bossblind': 'BossBlind',
          'tag': 'Tag',
          'smallblindtag': 'SmallBlindTag',
          'bigblindtag': 'BigBlindTag',
          'playingcard': 'PlayingCard',
          'standardcard': 'PlayingCard',
          'and': 'And',
          'or': 'Or'
        };
        return typeMap[lower] || type;
      };
      
      // Normalize all clause types recursively (handles nested And/Or clauses)
      const normalizeClauses = (clauses) => {
        if (!Array.isArray(clauses)) return [];
        return clauses.map(clause => {
          if (!clause || typeof clause !== 'object') return clause;
          
          // Normalize type
          if (clause.type) {
            clause.type = normalizeType(clause.type);
          }
          
          // Ensure antes is an array of numbers
          if (clause.antes !== undefined) {
            if (!Array.isArray(clause.antes)) {
              clause.antes = [clause.antes];
            }
            // Validate ante range (0-39)
            clause.antes = clause.antes.filter(a => typeof a === 'number' && a >= 0 && a <= 39);
          }
          
          // Recursively normalize nested clauses (for And/Or)
          if (clause.clauses && Array.isArray(clause.clauses)) {
            clause.clauses = normalizeClauses(clause.clauses);
          }
          
          return clause;
        });
      };
      
      config.must = normalizeClauses(config.must);
      config.should = normalizeClauses(config.should);
      config.mustNot = normalizeClauses(config.mustNot);

      return jsonResponse({
        success: true,
        config
      });

    } catch (error) {
      console.error('Worker error:', error);
      return jsonResponse({
        success: false,
        error: error.message || 'Internal server error'
      }, 500);
    }
  }
};

function jsonResponse(data, status = 200) {
  return new Response(JSON.stringify(data), {
    status,
    headers: {
      'Content-Type': 'application/json',
      'Access-Control-Allow-Origin': '*',
    },
  });
}
