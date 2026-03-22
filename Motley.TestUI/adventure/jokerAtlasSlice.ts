import * as THREE from 'three'
import { applyBalatroGridUV, getLoadedTexturePixelSize } from '../balatro/spriteAtlas/gridUV'
import type { BalatroJokerCenter } from '../balatro/spriteAtlas/jokerRegistry'
import { getJokerAtlasGridSize } from '../balatro/spriteAtlas/jokerRegistry'

/** Clone atlas texture with UVs for one P_CENTERS cell. Caller should `.dispose()` when done. */
export function cloneAtlasSliceForJoker(
  base: THREE.Texture,
  center: BalatroJokerCenter
): THREE.Texture {
  const tex = base.clone()
  tex.needsUpdate = true
  tex.colorSpace = THREE.SRGBColorSpace
  tex.magFilter = THREE.NearestFilter
  tex.minFilter = THREE.NearestFilter
  const { width: tw, height: th } = getLoadedTexturePixelSize(base.image)
  const grid = getJokerAtlasGridSize()
  const cellW = tw / grid.cols
  const cellH = th / grid.rows
  applyBalatroGridUV(tex, center.pos, {
    cellW,
    cellH,
    textureWidth: tw,
    textureHeight: th,
  })
  return tex
}

export function jokerPlaneSizeFromTexture(tex: THREE.Texture): { w: number; h: number } {
  const { width: tw, height: th } = getLoadedTexturePixelSize(tex.image)
  const grid = getJokerAtlasGridSize()
  const cellW = tw / grid.cols
  const cellH = th / grid.rows
  const aspect = cellW / cellH
  const h = 3.2
  return { w: aspect * h, h }
}
