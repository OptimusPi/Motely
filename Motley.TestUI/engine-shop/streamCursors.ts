/**
 * Balatro pseudoseed chains advance a **double** per queue id (`Game` → `Cache` → `Map`).
 * Motely’s per-stream state is the same *idea* (cursor = double); this names it in TS.
 */
export type StreamCursor = number

/** JSON-serializable snapshot of every touched queue’s cursor + shop first-pack flag. */
export type BalatroStreamStateJson = Readonly<{
  nodes: Record<string, StreamCursor>
  generatedFirstPack: boolean
}>
