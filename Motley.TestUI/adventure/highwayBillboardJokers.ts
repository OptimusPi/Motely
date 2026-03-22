import {
  findJokerByDisplayName,
  type BalatroJokerCenter,
} from '../balatro/spriteAtlas/jokerRegistry'
import { createTsGame } from '../engine-shop/tsBalatroGame'
import { Type } from '@balatrots/enum/cards/CardType'

export type HighwayBillboardResult = Readonly<{
  jokers: BalatroJokerCenter[]
  note: string | null
}>

/** Sample unique jokers from the TS engine shop stream (ante 1), same seed/deck/stake as the run. */
export function loadHighwayBillboardJokers(
  seed: string,
  deck: string,
  stake: string
): HighwayBillboardResult {
  const g = createTsGame(seed, deck, stake)
  const out: BalatroJokerCenter[] = []
  const seen = new Set<string>()
  for (let n = 0; n < 12_000 && out.length < 96; n++) {
    const s = g.nextShopItem(1)
    if (s.type !== Type.JOKER) continue
    const c = findJokerByDisplayName(s.item.getName())
    if (c && !seen.has(c.key)) {
      seen.add(c.key)
      out.push(c)
    }
  }
  if (out.length === 0) {
    return {
      jokers: [],
      note: 'No jokers mapped from TS shop stream (registry mismatch?).',
    }
  }
  return { jokers: out, note: null }
}
