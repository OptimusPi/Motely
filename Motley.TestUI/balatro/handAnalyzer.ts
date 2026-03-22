// Balatro Hand Analyzer - Detects poker hands and calculates scores
import {
  Card,
  HandType,
  HandScore,
  AnalysisResult,
  ScoreBreakdown,
  BASE_HANDS,
  RANK_CHIPS,
  Rank,
} from './types'
import { getRankValue, groupByRank, groupBySuit } from './deck'

interface HandDetectionResult {
  type: HandType
  scoringCards: Card[]
}

// Check if ranks form a straight (including wheel A-2-3-4-5)
function isStraight(ranks: Rank[]): boolean {
  const values = ranks.map((r) => getRankValue(r)).sort((a, b) => a - b)
  const uniqueValues = [...new Set(values)]

  if (uniqueValues.length < 5) return false

  // Check normal straight
  for (let i = 0; i <= uniqueValues.length - 5; i++) {
    let isSequential = true
    for (let j = 0; j < 4; j++) {
      if (uniqueValues[i + j + 1] - uniqueValues[i + j] !== 1) {
        isSequential = false
        break
      }
    }
    if (isSequential) return true
  }

  // Check wheel (A-2-3-4-5) - Ace is 14, so check for 2,3,4,5,14
  if (
    uniqueValues.includes(14) &&
    uniqueValues.includes(2) &&
    uniqueValues.includes(3) &&
    uniqueValues.includes(4) &&
    uniqueValues.includes(5)
  ) {
    return true
  }

  return false
}

// Get the best 5 cards for a straight
function getStraightCards(cards: Card[]): Card[] {
  const sorted = [...cards].sort((a, b) => getRankValue(b.rank) - getRankValue(a.rank))
  const values = sorted.map((c) => ({ card: c, value: getRankValue(c.rank) }))

  // Try to find highest straight
  for (let startValue = 14; startValue >= 5; startValue--) {
    const straightCards: Card[] = []
    for (let v = startValue; v > startValue - 5; v--) {
      const adjustedV = v === 1 ? 14 : v // Handle ace-low
      const found = values.find((x) => x.value === adjustedV && !straightCards.includes(x.card))
      if (found) straightCards.push(found.card)
    }
    if (straightCards.length === 5) return straightCards
  }

  // Check wheel
  const wheelValues = [14, 5, 4, 3, 2]
  const wheelCards: Card[] = []
  for (const v of wheelValues) {
    const found = values.find((x) => x.value === v && !wheelCards.includes(x.card))
    if (found) wheelCards.push(found.card)
  }
  if (wheelCards.length === 5) return wheelCards

  return sorted.slice(0, 5)
}

function detectHand(cards: Card[]): HandDetectionResult {
  if (cards.length === 0) {
    return { type: 'high_card', scoringCards: [] }
  }

  const rankGroups = groupByRank(cards)
  const suitGroups = groupBySuit(cards)

  // Count rank groups by size
  const groupSizes = Array.from(rankGroups.values())
    .map((g) => g.length)
    .sort((a, b) => b - a)

  // Check for flush (5+ cards of same suit)
  let flushSuit: Card[] | null = null
  for (const [, suitCards] of suitGroups) {
    if (suitCards.length >= 5) {
      flushSuit = suitCards
      break
    }
  }

  // Check for straight
  const allRanks = cards.map((c) => c.rank)
  const hasStraight = isStraight(allRanks)

  // FLUSH FIVE - 5 of same rank AND same suit (needs wild cards in Balatro)
  for (const [, rankCards] of rankGroups) {
    if (rankCards.length >= 5) {
      const sameSuit = rankCards.filter((c) => c.suit === rankCards[0].suit)
      if (sameSuit.length >= 5) {
        return { type: 'flush_five', scoringCards: sameSuit.slice(0, 5) }
      }
    }
  }

  // FIVE OF A KIND
  for (const [, rankCards] of rankGroups) {
    if (rankCards.length >= 5) {
      return { type: 'five_of_a_kind', scoringCards: rankCards.slice(0, 5) }
    }
  }

  // FLUSH HOUSE - Full house with all same suit
  if (groupSizes[0] >= 3 && groupSizes[1] >= 2 && flushSuit && flushSuit.length >= 5) {
    const flushRankGroups = groupByRank(flushSuit)
    const flushGroupSizes = Array.from(flushRankGroups.values())
      .map((g) => g.length)
      .sort((a, b) => b - a)
    if (flushGroupSizes[0] >= 3 && flushGroupSizes[1] >= 2) {
      const threeKind = Array.from(flushRankGroups.values())
        .find((g) => g.length >= 3)!
        .slice(0, 3)
      const pair = Array.from(flushRankGroups.values())
        .find((g) => g.length >= 2 && g[0].rank !== threeKind[0].rank)!
        .slice(0, 2)
      return { type: 'flush_house', scoringCards: [...threeKind, ...pair] }
    }
  }

  // ROYAL FLUSH
  if (flushSuit && flushSuit.length >= 5) {
    const flushRanks = flushSuit.map((c) => c.rank)
    const royalRanks: Rank[] = ['A', 'K', 'Q', 'J', '10']
    if (royalRanks.every((r) => flushRanks.includes(r))) {
      const royalCards = royalRanks.map((r) => flushSuit!.find((c) => c.rank === r)!)
      return { type: 'royal_flush', scoringCards: royalCards }
    }
  }

  // STRAIGHT FLUSH
  if (flushSuit && flushSuit.length >= 5 && isStraight(flushSuit.map((c) => c.rank))) {
    return { type: 'straight_flush', scoringCards: getStraightCards(flushSuit).slice(0, 5) }
  }

  // FOUR OF A KIND
  for (const [, rankCards] of rankGroups) {
    if (rankCards.length >= 4) {
      return { type: 'four_of_a_kind', scoringCards: rankCards.slice(0, 4) }
    }
  }

  // FULL HOUSE
  if (groupSizes[0] >= 3 && groupSizes[1] >= 2) {
    const threeKind = Array.from(rankGroups.values())
      .find((g) => g.length >= 3)!
      .slice(0, 3)
    const pair = Array.from(rankGroups.values())
      .find((g) => g.length >= 2 && g[0].rank !== threeKind[0].rank)!
      .slice(0, 2)
    return { type: 'full_house', scoringCards: [...threeKind, ...pair] }
  }

  // FLUSH
  if (flushSuit && flushSuit.length >= 5) {
    const sorted = [...flushSuit].sort((a, b) => getRankValue(b.rank) - getRankValue(a.rank))
    return { type: 'flush', scoringCards: sorted.slice(0, 5) }
  }

  // STRAIGHT
  if (hasStraight) {
    return { type: 'straight', scoringCards: getStraightCards(cards) }
  }

  // THREE OF A KIND
  for (const [, rankCards] of rankGroups) {
    if (rankCards.length >= 3) {
      return { type: 'three_of_a_kind', scoringCards: rankCards.slice(0, 3) }
    }
  }

  // TWO PAIR
  const pairs = Array.from(rankGroups.values()).filter((g) => g.length >= 2)
  if (pairs.length >= 2) {
    const sortedPairs = pairs.sort((a, b) => getRankValue(b[0].rank) - getRankValue(a[0].rank))
    return {
      type: 'two_pair',
      scoringCards: [...sortedPairs[0].slice(0, 2), ...sortedPairs[1].slice(0, 2)],
    }
  }

  // PAIR
  if (pairs.length >= 1) {
    return { type: 'pair', scoringCards: pairs[0].slice(0, 2) }
  }

  // HIGH CARD
  const sorted = [...cards].sort((a, b) => getRankValue(b.rank) - getRankValue(a.rank))
  return { type: 'high_card', scoringCards: [sorted[0]] }
}

export function analyzeHand(
  cards: Card[],
  handLevels: Partial<Record<HandType, number>> = {}
): AnalysisResult {
  const detection = detectHand(cards)
  const baseHand = BASE_HANDS[detection.type]
  const level = handLevels[detection.type] ?? 1

  // Level scaling: each level adds base values
  const levelBonus = level - 1
  const scaledChips = baseHand.chips + levelBonus * 10
  const scaledMult = baseHand.mult + levelBonus * 1

  const handScore: HandScore = {
    type: detection.type,
    name: baseHand.name,
    baseChips: scaledChips,
    baseMult: scaledMult,
    level,
  }

  // Calculate card chips
  let cardChips = 0
  const breakdown: ScoreBreakdown[] = []

  breakdown.push({
    source: `${baseHand.name} (Lvl ${level})`,
    chips: scaledChips,
    mult: scaledMult,
    description: `Base hand score`,
  })

  for (const card of detection.scoringCards) {
    const chips = RANK_CHIPS[card.rank]
    cardChips += chips

    // Add enhancement bonuses
    if (card.enhancement === 'bonus') {
      cardChips += 30
      breakdown.push({
        source: `${card.rank} Bonus`,
        chips: 30,
        description: 'Bonus card +30 chips',
      })
    }
    if (card.enhancement === 'mult') {
      breakdown.push({
        source: `${card.rank} Mult`,
        mult: 4,
        description: 'Mult card +4 mult',
      })
    }
  }

  breakdown.push({
    source: 'Scoring Cards',
    chips: cardChips,
    description: `Chip values from ${detection.scoringCards.length} cards`,
  })

  const totalChips = scaledChips + cardChips
  let totalMult = scaledMult

  // Add mult from mult cards
  for (const card of detection.scoringCards) {
    if (card.enhancement === 'mult') {
      totalMult += 4
    }
  }

  // Apply x multipliers from editions
  let xMult = 1
  for (const card of detection.scoringCards) {
    if (card.edition === 'foil') {
      // Foil adds chips, not mult
      breakdown.push({
        source: `${card.rank} Foil`,
        chips: 50,
        description: 'Foil +50 chips',
      })
    }
    if (card.edition === 'holographic') {
      totalMult += 10
      breakdown.push({
        source: `${card.rank} Holo`,
        mult: 10,
        description: 'Holographic +10 mult',
      })
    }
    if (card.edition === 'polychrome') {
      xMult *= 1.5
      breakdown.push({
        source: `${card.rank} Polychrome`,
        xMult: 1.5,
        description: 'Polychrome x1.5 mult',
      })
    }
  }

  const finalScore = Math.floor(totalChips * totalMult * xMult)

  return {
    hand: handScore,
    scoringCards: detection.scoringCards,
    totalChips,
    totalMult: totalMult * xMult,
    finalScore,
    breakdown,
  }
}

// Get all possible hands from a set of cards (for hand preview)
export function getPossibleHands(cards: Card[]): Map<HandType, Card[]> {
  const possible = new Map<HandType, Card[]>()

  // Generate all combinations of 1-5 cards
  const combinations: Card[][] = []

  function combine(start: number, combo: Card[]) {
    if (combo.length > 0 && combo.length <= 5) {
      combinations.push([...combo])
    }
    if (combo.length >= 5) return

    for (let i = start; i < cards.length; i++) {
      combo.push(cards[i])
      combine(i + 1, combo)
      combo.pop()
    }
  }

  combine(0, [])

  // Analyze each combination
  for (const combo of combinations) {
    const result = detectHand(combo)
    if (!possible.has(result.type) || possible.get(result.type)!.length < combo.length) {
      possible.set(result.type, combo)
    }
  }

  return possible
}
