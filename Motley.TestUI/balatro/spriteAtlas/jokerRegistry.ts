import jokersPayload from '../../data/balatro-jokers.json'

export type BalatroJokerCenter = {
  key: string
  name: string
  pos: { x: number; y: number }
  soulPos?: { x: number; y: number }
}

export const BALATRO_JOKER_ATLAS = jokersPayload.atlas

export const BALATRO_JOKERS: BalatroJokerCenter[] = jokersPayload.jokers

/** Sheet columns/rows for `Jokers.png` (max P_CENTERS cell index + 1, including soul overlays). */
export function getJokerAtlasGridSize(): { cols: number; rows: number } {
  let mx = 0
  let my = 0
  for (const j of BALATRO_JOKERS) {
    mx = Math.max(mx, j.pos.x)
    my = Math.max(my, j.pos.y)
    if (j.soulPos) {
      mx = Math.max(mx, j.soulPos.x)
      my = Math.max(my, j.soulPos.y)
    }
  }
  return { cols: mx + 1, rows: my + 1 }
}

/** Wiki / classifier typos vs in-game localization strings */
const NAME_ALIASES: Record<string, string> = {
  canio: 'Caino',
  séance: 'Seance',
  seance: 'Seance',
}

function normalizeLookup(name: string): string {
  const t = name.trim()
  const alias = NAME_ALIASES[t.toLowerCase()]
  return alias ?? t
}

export function findJokerByDisplayName(displayName: string): BalatroJokerCenter | undefined {
  const needle = normalizeLookup(displayName).toLowerCase()
  return BALATRO_JOKERS.find((j) => j.name.toLowerCase() === needle)
}

export function findJokerByKey(key: string): BalatroJokerCenter | undefined {
  const k = key.trim().toLowerCase()
  return BALATRO_JOKERS.find((j) => j.key.toLowerCase() === k)
}
