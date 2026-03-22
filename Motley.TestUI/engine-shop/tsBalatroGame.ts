import { Game } from '@balatrots/Game'
import { Deck, DeckType } from '@balatrots/enum/Deck'
import { Stake, StakeType } from '@balatrots/enum/Stake'
import { InstanceParams } from '@balatrots/struct/InstanceParams'

/** JAML-style labels → engine enums (same as highway billboards). */
export const TS_DECK_MAP: Record<string, DeckType> = {
  Red: DeckType.RED_DECK,
  Blue: DeckType.BLUE_DECK,
  Yellow: DeckType.YELLOW_DECK,
  Green: DeckType.GREEN_DECK,
  Black: DeckType.BLACK_DECK,
  Magic: DeckType.MAGIC_DECK,
  Nebula: DeckType.NEBULA_DECK,
  Ghost: DeckType.GHOST_DECK,
  Abandoned: DeckType.ABANDONED_DECK,
  Checkered: DeckType.CHECKERED_DECK,
  Zodiac: DeckType.ZODIAC_DECK,
  Painted: DeckType.PAINTED_DECK,
  Anaglyph: DeckType.ANAGLYPH_DECK,
  Plasma: DeckType.PLASMA_DECK,
  Erratic: DeckType.ERRATIC_DECK,
}

export const TS_STAKE_MAP: Record<string, StakeType> = {
  White: StakeType.WHITE_STAKE,
  Red: StakeType.RED_STAKE,
  Green: StakeType.GREEN_STAKE,
  Black: StakeType.BLACK_STAKE,
  Blue: StakeType.BLUE_STAKE,
  Purple: StakeType.PURPLE_STAKE,
  Orange: StakeType.ORANGE_STAKE,
  Gold: StakeType.GOLD_STAKE,
}

export function createTsGame(seed: string, deck: string, stake: string): Game {
  return new Game(
    seed.trim() || 'BALATRO1',
    new InstanceParams(
      new Deck(TS_DECK_MAP[deck] ?? DeckType.RED_DECK),
      new Stake(TS_STAKE_MAP[stake] ?? StakeType.WHITE_STAKE)
    )
  )
}
