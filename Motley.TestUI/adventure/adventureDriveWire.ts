/**
 * Compact wire format for co-op highway drive (Upstash REST).
 * `k` = WASD pressed as bits [w,s,a,d] — mostly for debugging / future replay.
 */
export type DriveSnapWire = {
  p: [number, number, number]
  q: [number, number, number, number]
  v: [number, number, number]
  o: [number, number, number]
  s: number
  k: [0 | 1, 0 | 1, 0 | 1, 0 | 1]
}

export type DriveRoomDoc = {
  updatedAt: number
  snap: DriveSnapWire
}

export function keysToBits(k: { w: boolean; s: boolean; a: boolean; d: boolean }): DriveSnapWire['k'] {
  return [k.w ? 1 : 0, k.s ? 1 : 0, k.a ? 1 : 0, k.d ? 1 : 0]
}
