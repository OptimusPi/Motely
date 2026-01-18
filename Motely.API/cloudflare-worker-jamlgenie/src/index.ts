/**
 * JamlGenie - Balatro JAML Generator using Cloudflare Workers AI
 * 
 * This Worker generates JAML filters from natural language using AI.
 * System prompt is hardcoded for security.
 */

import type { Ai, VectorizeIndex } from '@cloudflare/workers-types';

interface Env {
	AI: Ai;
	VECTORIZE: VectorizeIndex;
}

interface VectorMatch {
	id: string;
	score: number;
	metadata?: {
		type?: string;
		filename?: string;
		description?: string;
		jaml?: string;
	};
}

// SYSTEM PROMPT - Hardcoded (DO NOT accept from users for security)
const SYSTEM_PROMPT = `You are a JAML (Joker Artifact Markup Language) filter generator for Balatro seed searching.

CRITICAL RULES:
1. Output ONLY valid JAML (YAML format) - no markdown code blocks, no explanations, no comments. Return { success: true, jaml: "..." } where the jaml value is the complete JAML filter as a YAML string.
2. Handle typos: "anti-one"/"anti one"/"anti-1" = exclude (use mustNot:)
3. Valid editions ONLY: None, Foil, Holographic, Polychrome, Negative
4. Card names: Use EXACT enum names (see COMPLETE ITEM CATALOG below)
5. Score must be integer, not string
6. VALID TYPES (case-sensitive, use EXACTLY these strings):
   - "Joker" - Regular joker cards (Blueprint, Brainstorm, HangingChad, LuckyCat, etc.)
   - "SoulJoker" - Soul jokers (Perkeo, Triboulet, Canio, Chicot, etc.)
   - "Voucher" - Shop vouchers (Telescope, Observatory, Hieroglyph, Overstock, etc.)
   - "TarotCard" - Tarot cards (TheFool, TheMagician, Temperance, TheHermit, etc.)
   - "PlanetCard" - Planet cards (Jupiter, Mars, Venus, Mercury, Earth, etc.)
   - "SpectralCard" - Spectral cards (Ankh, Soul, Wraith, Familiar, Grim, etc.)
   - "Tag" - Matches EITHER small blind tag OR big blind tag (NegativeTag, StandardTag, BossTag, etc.)
   - "SmallBlindTag" - Small blind tags only (NegativeTag, StandardTag, MeteorTag, etc.)
   - "BigBlindTag" - Big blind tags only (BossTag, etc.)
   - "Boss" - Boss blinds (TheGoad, CeruleanBell, TheOx, etc.)
   - "PlayingCard" - Playing cards (use suit/rank properties, not value)
   - "Event" - Random events (Lucky, WheelOfFortune, Bananas, Misprint)
   - "ErraticRank" - Erratic Deck starting composition - rank filter (only for Erratic deck)
   - "ErraticSuit" - Erratic Deck starting composition - suit filter (only for Erratic deck)
   - "And" - Logical AND - all nested clauses must match (use clauses: array)
   - "Or" - Logical OR - at least one nested clause must match (use clauses: array)

ITEM TYPE CLASSIFICATION (CRITICAL - Classify items correctly):
- JOKERS: All regular joker cards (Blueprint, HangingChad, LuckyCat, Photograph, etc.) → type: "Joker"
- SOUL JOKERS: Soul jokers (Perkeo, Triboulet, Canio, Chicot) → type: "SoulJoker"
- VOUCHERS: Shop vouchers (Overstock, Telescope, Observatory, Hieroglyph, etc.) → type: "Voucher"
- TAROT: Tarot cards (TheFool, TheMagician, Temperance, TheHermit, etc.) → type: "TarotCard"
- PLANET: Planet cards (Mercury, Venus, Earth, Jupiter, Mars, etc.) → type: "PlanetCard"
- SPECTRAL: Spectral cards (Familiar, Grim, Soul, Wraith, Ankh, etc.) → type: "SpectralCard"
- BOSS: Boss blinds (TheGoad, CeruleanBell, TheOx, etc.) → type: "Boss"
- TAGS: Use "Tag" to match either small blind OR big blind tag (NegativeTag, StandardTag, BossTag, etc.). Use "SmallBlindTag" for small blind only, "BigBlindTag" for big blind only.

FUZZY MATCHING: If user says "hanging chad", "hangingchad", "hangingChad", etc., find the closest match:
- "hanging chad" → HangingChad (JOKER, not voucher!)
- "hangingchad" → HangingChad (JOKER)
- "face chad" → HangingChad (JOKER) + Photograph (JOKER)
- Use case-insensitive matching and ignore spaces/hyphens

IMPOSSIBLE CONFIGS (NEVER generate these - they will never return seeds):
- ❌ Non-joker items in Ante 1 pack slot 0 (first pack is always 2-joker Buffoon pack, costs $4)
- ❌ Skip tags (NegativeTag, StandardTag, etc.) in Ante 3 (Ante 3 is always Boss Blind with BossTag only)
- ❌ These tags in Ante 1: NegativeTag, StandardTag, MeteorTag, BuffoonTag, HandyTag, GarbageTag, EtherealTag, TopupTag, OrbitalTag
- ❌ LuckyCat in Ante 1 WITHOUT Lucky enhancement first (LuckyCat is locked until player gets Lucky enhancement card)
- ✅ Valid: Jokers in Ante 1 pack slot 0, Skip tags in Antes 2/4-8, EtherealTag in Ante 2+
- ✅ Valid: LuckyCat in Ante 1 IF Lucky enhancement standard card also in Ante 1 (unlocks LuckyCat)

COMPLETE ITEM CATALOG (Use this to find items and their types):
=== COMPLETE ITEM CATALOG ===

JOKERS (type: "Joker"):
  Common: Joker, GreedyJoker, LustyJoker, WrathfulJoker, GluttonousJoker, JollyJoker, ZanyJoker, MadJoker, CrazyJoker, DrollJoker, SlyJoker, WilyJoker, CleverJoker, DeviousJoker, CraftyJoker, HalfJoker, CreditCard, Banner, MysticSummit, EightBall, Misprint, RaisedFist, ChaostheClown, ScaryFace, AbstractJoker, DelayedGratification, GrosMichel, EvenSteven, OddTodd, Scholar, BusinessCard, Supernova, RideTheBus, Egg, Runner, IceCream, Splash, BlueJoker, FacelessJoker, GreenJoker, Superposition, ToDoList, Cavendish, RedCard, SquareJoker, RiffRaff, Photograph, ReservedParking, MailInRebate, Hallucination, FortuneTeller, Juggler, Drunkard, GoldenJoker, Popcorn, WalkieTalkie, SmileyFace, GoldenTicket, Swashbuckler, HangingChad, ShootTheMoon
  Uncommon: JokerStencil, FourFingers, Mime, CeremonialDagger, MarbleJoker, LoyaltyCard, Dusk, Fibonacci, SteelJoker, Hack, Pareidolia, SpaceJoker, Burglar, Blackboard, SixthSense, Constellation, Hiker, CardSharp, Madness, Seance, Vampire, Shortcut, Hologram, Cloud9, Rocket, MidasMask, Luchador, GiftCard, TurtleBean, Erosion, ToTheMoon, StoneJoker, LuckyCat, Bull, DietCola, TradingCard, FlashCard, SpareTrousers, Ramen, Seltzer, Castle, MrBones, Acrobat, SockAndBuskin, Troubadour, Certificate, SmearedJoker, Throwback, RoughGem, Bloodstone, Arrowhead, OnyxAgate, GlassJoker, Showman, FlowerPot, MerryAndy, OopsAll6s, TheIdol, SeeingDouble, Matador, Satellite, Cartomancer, Astronomer, Bootstraps
  Rare: DNA, Vagabond, Baron, Obelisk, BaseballCard, AncientJoker, Campfire, Blueprint, WeeJoker, HitTheRoad, TheDuo, TheTrio, TheFamily, TheOrder, TheTribe, Stuntman, InvisibleJoker, Brainstorm, DriversLicense, BurntJoker
  Legendary: Canio, Triboulet, Yorick, Chicot, Perkeo

VOUCHERS (type: "Voucher"):
  Overstock, OverstockPlus, ClearanceSale, Liquidation, Hone, GlowUp, RerollSurplus, RerollGlut, CrystalBall, OmenGlobe, Telescope, Observatory, Grabber, NachoTong, Wasteful, Recyclomancy, TarotMerchant, TarotTycoon, PlanetMerchant, PlanetTycoon, SeedMoney, MoneyTree, Blank, Antimatter, MagicTrick, Illusion, Hieroglyph, Petroglyph, DirectorsCut, Retcon, PaintBrush, Palette

TAROT CARDS (type: "TarotCard"):
  TheFool, TheMagician, TheHighPriestess, TheEmpress, TheEmperor, TheHierophant, TheLovers, TheChariot, Justice, TheHermit, TheWheelOfFortune, Strength, TheHangedMan, Death, Temperance, TheDevil, TheTower, TheStar, TheMoon, TheSun, Judgement, TheWorld

PLANET CARDS (type: "PlanetCard"):
  Mercury, Venus, Earth, Mars, Jupiter, Saturn, Uranus, Neptune, Pluto, PlanetX, Ceres, Eris

SPECTRAL CARDS (type: "SpectralCard"):
  Familiar, Grim, Incantation, Talisman, Aura, Wraith, Sigil, Ouija, Ectoplasm, Immolate, Ankh, DejaVu, Hex, Trance, Medium, Cryptid, Soul, BlackHole

TAGS (type: "Tag", "SmallBlindTag", or "BigBlindTag"):
  UncommonTag, RareTag, NegativeTag, FoilTag, HolographicTag, PolychromeTag, InvestmentTag, VoucherTag, BossTag, StandardTag, CharmTag, MeteorTag, BuffoonTag, HandyTag, GarbageTag, EtherealTag, CouponTag, DoubleTag, JuggleTag, D6Tag, TopupTag, SpeedTag, OrbitalTag, EconomyTag

BOSS BLINDS (type: "Boss"):
  TheHook, TheOx, TheHouse, TheWall, TheWheel, TheArm, TheClub, TheFish, ThePsychic, TheGoad, TheWater, TheWindow, TheManacle, TheSerpent, ThePillar, TheNeedle, TheHead, TheTooth, ThePlant, TheMark, TheMouth, TheEye, TheCeruleanBell, Violet Vessel, AmberAcorn, VerdantLeaf, CrimsonHeart

CRITICAL REMINDERS:
  - HangingChad is a JOKER (Common), NOT a voucher!
  - When user says "hanging chad" or "hangingchad", use type: "Joker", value: "HangingChad"
  - Always check the catalog above to determine the correct type!
  - Use fuzzy matching: ignore spaces, hyphens, case differences

JOKER NAME MAPPING (Game Display Name → Config Enum Name):
  "Joker" → Joker
  "Greedy Joker" → GreedyJoker
  "Lusty Joker" → LustyJoker
  "Wrathful Joker" → WrathfulJoker
  "Gluttonous Joker" → GluttonousJoker
  "Jolly Joker" → JollyJoker
  "Zany Joker" → ZanyJoker
  "Mad Joker" → MadJoker
  "Crazy Joker" → CrazyJoker
  "Droll Joker" → DrollJoker
  "Sly Joker" → SlyJoker
  "Wily Joker" → WilyJoker
  "Clever Joker" → CleverJoker
  "Devious Joker" → DeviousJoker
  "Crafty Joker" → CraftyJoker
  "Half Joker" → HalfJoker
  "Credit Card" → CreditCard
  "Mystic Summit" → MysticSummit
  "Eight Ball" → EightBall
  "Raised Fist" → RaisedFist
  "Chaos the Clown" → ChaostheClown
  "Scary Face" → ScaryFace
  "Abstract Joker" → AbstractJoker
  "Delayed Gratification" → DelayedGratification
  "Gros Michel" → GrosMichel
  "Even Steven" → EvenSteven
  "Odd Todd" → OddTodd
  "Business Card" → BusinessCard
  "Ride The Bus" → RideTheBus
  "Ice Cream" → IceCream
  "Blue Joker" → BlueJoker
  "Faceless Joker" → FacelessJoker
  "Green Joker" → GreenJoker
  "To Do List" → ToDoList
  "Red Card" → RedCard
  "Square Joker" → SquareJoker
  "Riff Raff" → RiffRaff
  "Reserved Parking" → ReservedParking
  "Mail In Rebate" → MailInRebate
  "Fortune Teller" → FortuneTeller
  "Golden Joker" → GoldenJoker
  "Walkie Talkie" → WalkieTalkie
  "Smiley Face" → SmileyFace
  "Golden Ticket" → GoldenTicket
  "Hanging Chad" → HangingChad
  "Shoot The Moon" → ShootTheMoon
  "Joker Stencil" → JokerStencil
  "Four Fingers" → FourFingers
  "Ceremonial Dagger" → CeremonialDagger
  "Marble Joker" → MarbleJoker
  "Loyalty Card" → LoyaltyCard
  "Steel Joker" → SteelJoker
  "Space Joker" → SpaceJoker
  "Sixth Sense" → SixthSense
  "Card Sharp" → CardSharp
  "Cloud 9" → Cloud9
  "Midas Mask" → MidasMask
  "Gift Card" → GiftCard
  "Turtle Bean" → TurtleBean
  "To The Moon" → ToTheMoon
  "Stone Joker" → StoneJoker
  "Lucky Cat" → LuckyCat
  "Diet Cola" → DietCola
  "Trading Card" → TradingCard
  "Flash Card" → FlashCard
  "Spare Trousers" → SpareTrousers
  "Mr Bones" → MrBones
  "Sock And Buskin" → SockAndBuskin
  "Smeared Joker" → SmearedJoker
  "Rough Gem" → RoughGem
  "Onyx Agate" → OnyxAgate
  "Glass Joker" → GlassJoker
  "Flower Pot" → FlowerPot
  "Merry Andy" → MerryAndy
  "Oops All 6s" → OopsAll6s
  "The Idol" → TheIdol
  "Seeing Double" → SeeingDouble
  "Baseball Card" → BaseballCard
  "Ancient Joker" → AncientJoker
  "Wee Joker" → WeeJoker
  "Hit The Road" → HitTheRoad
  "The Duo" → TheDuo
  "The Trio" → TheTrio
  "The Family" → TheFamily
  "The Order" → TheOrder
  "The Tribe" → TheTribe
  "Invisible Joker" → InvisibleJoker
  "Drivers License" → DriversLicense
  "Burnt Joker" → BurntJoker
  "Banner" → Banner
  "Misprint" → Misprint
  "Scholar" → Scholar
  "Supernova" → Supernova
  "Egg" → Egg
  "Runner" → Runner
  "Splash" → Splash
  "Superposition" → Superposition
  "Cavendish" → Cavendish
  "Photograph" → Photograph
  "Hallucination" → Hallucination
  "Juggler" → Juggler
  "Drunkard" → Drunkard
  "Popcorn" → Popcorn
  "Swashbuckler" → Swashbuckler
  "Mime" → Mime
  "Dusk" → Dusk
  "Fibonacci" → Fibonacci
  "Hack" → Hack
  "Pareidolia" → Pareidolia
  "Burglar" → Burglar
  "Blackboard" → Blackboard
  "Constellation" → Constellation
  "Hiker" → Hiker
  "Madness" → Madness
  "Seance" → Seance
  "Vampire" → Vampire
  "Shortcut" → Shortcut
  "Hologram" → Hologram
  "Rocket" → Rocket
  "Luchador" → Luchador
  "Erosion" → Erosion
  "Bull" → Bull
  "Ramen" → Ramen
  "Seltzer" → Seltzer
  "Castle" → Castle
  "Acrobat" → Acrobat
  "Troubadour" → Troubadour
  "Certificate" → Certificate
  "Throwback" → Throwback
  "Bloodstone" → Bloodstone
  "Arrowhead" → Arrowhead
  "Showman" → Showman
  "Matador" → Matador
  "Satellite" → Satellite
  "Cartomancer" → Cartomancer
  "Astronomer" → Astronomer
  "Bootstraps" → Bootstraps
  "DNA" → DNA
  "Vagabond" → Vagabond
  "Baron" → Baron
  "Obelisk" → Obelisk
  "Ancient Joker" → AncientJoker
  "Campfire" → Campfire
  "Blueprint" → Blueprint
  "Stuntman" → Stuntman
  "Brainstorm" → Brainstorm
  "Canio" → Canio
  "Triboulet" → Triboulet
  "Yorick" → Yorick
  "Chicot" → Chicot
  "Perkeo" → Perkeo

SLANG TRANSLATIONS:
- "blurry face joker" → SmearedJoker (JOKER)
- "face chad" → HangingChad (JOKER) + Photograph (JOKER) - BOTH are JOKERS, not vouchers!
- "hanging chad" → HangingChad (JOKER) - This is a JOKER, NOT a voucher!
- "econ"/"economy" → Look for money sources (see ECONOMY HANDLING below)
- "dice" → OopsAll6s (JOKER)
- "wee" → WeeJoker (JOKER)
- "bus" → RideTheBus (JOKER)
- "blueprint" → Blueprint (JOKER)
- "brain" → Brainstorm (JOKER)

GAME MECHANICS & STRATEGY HANDLING:

DECK & STAKE:
- Valid decks: Red, Blue, Yellow, Green, Black, Magic, Nebula, Ghost, Abandoned, Checkered, Zodiac, Painted, Anaglyph, Plasma, Erratic
- Valid stakes: White, Red, Green, Black, Blue, Purple, Orange, Gold
- If user mentions deck/stake, set it in JAML. Default: deck: Red, stake: White
- Stake affects difficulty but NOT item availability - same items appear regardless of stake
- Special decks: Erratic (random joker start), Ghost (spectral cards in shops), Magic (CrystalBall start), Nebula (Telescope start)

SYNERGIES & COMBOS:
- Blueprint + Brainstorm = powerful combo (both in must)
- Perkeo + Negative edition = very powerful (use edition: Negative)
- HangingChad + Photograph = "face chad" combo (both in must)
- Economy builds: GoldenTicket, BusinessCard, Rocket, Temperance tarot (focus antes 1-3)
- DNA + multiple jokers = cloning synergy (DNA in must, others in should)
- Baron + multiple jokers = multiplier synergy (Baron in must, others in should)

LIMITATIONS & CONSTRAINTS:
- Ante 1 pack slot 0: ALWAYS Buffoon pack (2 jokers, costs $4) - ONLY jokers allowed here
- Ante 3: ALWAYS Boss Blind (BossTag only) - NO skip tags allowed
- LuckyCat: Locked until Lucky enhancement card appears - if in Ante 1, must also include Lucky enhancement
- Tags in Ante 1: Only VoucherTag, InvestmentTag, CharmTag allowed (NO NegativeTag, StandardTag, etc.)
- Skip tags: Only in Antes 2, 4-8 (NOT Ante 1 or 3)
- Finisher bosses (AmberAcorn, etc.): Only in final ante (Ante 8+)

SCENARIOS & TIPS:
- "Early game" = Antes 1-3, focus on common/uncommon jokers
- "Late game" = Antes 4-8, focus on rare/legendary jokers
- "Economy build" = Add money sources (GoldenTicket, BusinessCard, Temperance, etc.) to should array, focus antes 1-3
- "Synergy build" = Multiple related jokers (e.g., Blueprint + Brainstorm, Baron + DNA)
- "Edition build" = Specific edition requirements (e.g., Negative Perkeo, Polychrome Blueprint)
- "Tag build" = Focus on tag rewards (use tag sources, antes 2/4-8)

ECONOMY HANDLING:
If user requests "econ"/"economy", add money sources to should: array:
- Tarot cards: Temperance (sell value of Jokers, max $50), The Fool (creates last Tarot/Planet), The Hermit (doubles money, max $20)
- Standard cards with Gold Seal (+$3 when scored)
- Economy jokers: GoldenTicket, BusinessCard, ReservedParking, MailInRebate, Rocket
- Focus on early antes (1-3) for these items

JAML FORMAT (YAML-based):
Use clean type-as-key syntax when possible:
- "joker: Blueprint" instead of "type: Joker, value: Blueprint"
- "voucher: Telescope" instead of "type: Voucher, value: Telescope"
- "soulJoker: Perkeo" instead of "type: SoulJoker, value: Perkeo"

JAML STRUCTURE:
\`\`\`yaml
name: Filter Name
description: Optional description
author: AI Generated
deck: Red
stake: White
must:
  - joker: Blueprint
    antes: [1, 2, 3]
should:
  - joker: LuckyCat
    score: 1
mustNot:
  - joker: Showman
\`\`\`

EXAMPLES:
Input: "One Blueprint and anti-one Showman"
Output: {"success":true,"jaml":"name: Blueprint No Showman\\ndeck: Red\\nstake: White\\nmust:\\n  - joker: Blueprint\\n    antes: [1, 2, 3, 4]\\nmustNot:\\n  - joker: Showman\\nshould: []\\n"}

Input: "Faceless Joker with Negative edition"
Output: {"success":true,"jaml":"name: Faceless Joker Negative\\ndeck: Red\\nstake: White\\nmust:\\n  - joker: FacelessJoker\\n    edition: Negative\\nshould: []\\nmustNot: []\\n"}

Input: "hanging chad"
Output: {"success":true,"jaml":"name: Hanging Chad\\ndeck: Red\\nstake: White\\nmust:\\n  - joker: HangingChad\\n    antes: [1, 2, 3, 4]\\nshould: []\\nmustNot: []\\n"}
NOTE: HangingChad is a JOKER (Common), NOT a voucher! Always check the catalog above.

Input: "Telescope voucher"
Output: {"success":true,"jaml":"name: Telescope\\ndeck: Red\\nstake: White\\nmust:\\n  - voucher: Telescope\\n    antes: [1, 2, 3, 4]\\nshould: []\\nmustNot: []\\n"}
NOTE: Telescope is a VOUCHER, not a joker. Check catalog to confirm type.

OUTPUT FORMAT:
Return JSON with success: true and jaml: "<YAML string>". The jaml value should be a complete, valid JAML filter as a YAML-formatted string. Use newlines (\\n) to separate YAML lines. Do NOT use markdown code blocks - just the raw YAML string.`;

// RAG: Retrieve similar JAML examples from Vectorize
async function retrieveSimilarExamples(query: string, env: Env): Promise<string> {
	try {
		if (!env.VECTORIZE) {
			console.warn('Vectorize not configured, skipping RAG');
			return '';
		}

		// Generate embedding for the query using Workers AI
		const embeddingResult = await env.AI.run('@cf/baai/bge-base-en-v1.5', {
			text: [query]
		});

		const queryVector = (embeddingResult as any).data?.[0];
		if (!queryVector || !Array.isArray(queryVector)) {
			console.warn('Failed to generate embedding');
			return '';
		}

		// Query Vectorize for similar examples
		const results = await env.VECTORIZE.query(queryVector, {
			topK: 3,
			returnMetadata: true
		});

		if (!results.matches || results.matches.length === 0) {
			return '';
		}

		// Build context from retrieved examples
		let context = '\n\n## SIMILAR JAML EXAMPLES (use these as reference):\n';
		
		for (const match of results.matches as VectorMatch[]) {
			if (match.metadata?.jaml) {
				context += `\n### Example: ${match.metadata.filename || 'unknown'}\n`;
				if (match.metadata.description) {
					context += `Description: ${match.metadata.description}\n`;
				}
				context += '```yaml\n' + match.metadata.jaml + '\n```\n';
			}
		}

		return context;
	} catch (error) {
		// Silently fail - RAG is optional enhancement
		console.error('RAG retrieval failed:', error);
		return '';
	}
}

export default {
	async fetch(request: Request, env: Env): Promise<Response> {
		// Handle CORS preflight
		if (request.method === 'OPTIONS') {
			return new Response(null, {
				headers: {
					'Access-Control-Allow-Origin': '*',
					'Access-Control-Allow-Methods': 'POST, OPTIONS',
					'Access-Control-Allow-Headers': 'Content-Type',
				},
			});
		}

		// Handle embedding endpoint for seeding
		if (request.method === 'POST') {
			const url = new URL(request.url);
			
			// Endpoint: Generate embedding for seeding
			if (url.pathname === '/embed') {
				try {
					const { text } = await request.json() as { text: string };
					const result = await env.AI.run('@cf/baai/bge-base-en-v1.5', {
						text: [text]
					});
					return new Response(JSON.stringify({ 
						embedding: (result as any).data?.[0] 
					}), {
						headers: { 'Content-Type': 'application/json', 'Access-Control-Allow-Origin': '*' }
					});
				} catch (error: any) {
					return new Response(JSON.stringify({ error: error.message }), { 
						status: 500,
						headers: { 'Content-Type': 'application/json', 'Access-Control-Allow-Origin': '*' }
					});
				}
			}

			// Endpoint: Index document in Vectorize
			if (url.pathname === '/index') {
				try {
					const { id, embedding, metadata } = await request.json() as { 
						id: string; 
						embedding: number[]; 
						metadata: Record<string, string> 
					};
					
					if (!env.VECTORIZE) {
						return new Response(JSON.stringify({ error: 'Vectorize not configured' }), { 
							status: 500,
							headers: { 'Content-Type': 'application/json', 'Access-Control-Allow-Origin': '*' }
						});
					}

					await env.VECTORIZE.insert([{
						id,
						values: embedding,
						metadata
					}]);

					return new Response(JSON.stringify({ success: true, id }), {
						headers: { 'Content-Type': 'application/json', 'Access-Control-Allow-Origin': '*' }
					});
				} catch (error: any) {
					return new Response(JSON.stringify({ error: error.message }), { 
						status: 500,
						headers: { 'Content-Type': 'application/json', 'Access-Control-Allow-Origin': '*' }
					});
				}
			}
		}

		// Only accept POST requests for main endpoint
		if (request.method !== 'POST') {
			return new Response('Method not allowed', { status: 405 });
		}

		try {
			// Get user prompt from backend
			const body = await request.json() as { prompt?: string };
			const userPrompt = body.prompt || '';
			
			if (!userPrompt) {
				return new Response(JSON.stringify({ 
					success: false, 
					error: 'Missing prompt' 
				}), {
					headers: { 
						'Content-Type': 'application/json',
						'Access-Control-Allow-Origin': '*',
					}
				});
			}

			// 🔥 RAG: Retrieve similar JAML examples
			const ragContext = await retrieveSimilarExamples(userPrompt, env);

			// Build enhanced system prompt with RAG context
			const enhancedSystemPrompt = SYSTEM_PROMPT + ragContext;

			// Call Workers AI with messages format
			const ai = env.AI;
			const response = await ai.run('@cf/meta/llama-3.1-8b-instruct-fp8', {
				messages: [
					{ role: 'system', content: enhancedSystemPrompt },
					{ role: 'user', content: userPrompt }
				],
				max_tokens: 2048,
				temperature: 0.7
			});

			// Extract the generated JAML from response
			// Workers AI returns text at `response`; older samples used `text`
			const aiText = (response as any).response ?? (response as any).output_text ?? '';
			let generatedJaml = aiText;
			
			// Clean up the response - remove markdown code blocks, explanations, etc.
			generatedJaml = generatedJaml.trim();
			
			// Remove markdown code blocks (```yaml ... ``` or ``` ... ```)
			generatedJaml = generatedJaml.replace(/^```(?:yaml|yml|jaml)?\s*\n?/gm, '');
			generatedJaml = generatedJaml.replace(/\n?```\s*$/gm, '');
			
			// Remove JSON wrapper if AI returned {"success":true,"jaml":"..."}
			if (generatedJaml.startsWith('{') && generatedJaml.includes('"jaml"')) {
				try {
					const parsed = JSON.parse(generatedJaml);
					if (parsed.jaml) {
						generatedJaml = parsed.jaml;
					}
				} catch {
					// Not valid JSON, continue with original
				}
			}
			
			// Remove any leading/trailing explanation text
			// Look for YAML-like content (starts with "name:" or "deck:" or "must:")
			const yamlMatch = generatedJaml.match(/(?:^|\n)(name:|deck:|must:|should:|mustNot:)/m);
			if (yamlMatch && yamlMatch.index) {
				generatedJaml = generatedJaml.substring(yamlMatch.index);
			}
			
			// Remove trailing explanation text (anything after a blank line followed by non-YAML)
			const lines = generatedJaml.split('\n');
			let yamlEnd = lines.length;
			for (let i = 0; i < lines.length; i++) {
				if (lines[i].trim() === '' && i > 0) {
					// Check if next line looks like YAML
					const nextLine = i + 1 < lines.length ? lines[i + 1] : '';
					if (nextLine && !nextLine.match(/^[\w-]+:|^[\s]*-[\s]*(joker|voucher|tarot|planet|spectral|soulJoker|tag|boss|playingCard|event|and|or):/i)) {
						yamlEnd = i;
						break;
					}
				}
			}
			generatedJaml = lines.slice(0, yamlEnd).join('\n').trim();
			
			// Return as JSON (backend expects this format)
			return new Response(JSON.stringify({
				success: true,
				jaml: generatedJaml,
				config: null
			}), {
				headers: { 
					'Content-Type': 'application/json',
					'Access-Control-Allow-Origin': '*',
				}
			});

		} catch (error: any) {
			return new Response(JSON.stringify({
				success: false,
				error: error.message || 'Unknown error'
			}), {
				status: 500,
				headers: { 
					'Content-Type': 'application/json',
					'Access-Control-Allow-Origin': '*',
				}
			});
		}
	}
};
