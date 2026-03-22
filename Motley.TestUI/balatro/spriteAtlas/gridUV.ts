import * as THREE from 'three'

export type GridPos = { x: number; y: number }

/** Pixel size of a loaded texture source (TextureLoader → HTMLImageElement). No `as` casts. */
export function getLoadedTexturePixelSize(image: THREE.Texture['image']): {
  width: number
  height: number
} {
  if (image instanceof HTMLImageElement) {
    return {
      width: image.naturalWidth || image.width,
      height: image.naturalHeight || image.height,
    }
  }
  if (image instanceof HTMLCanvasElement) {
    return { width: image.width, height: image.height }
  }
  if (typeof ImageBitmap !== 'undefined' && image instanceof ImageBitmap) {
    return { width: image.width, height: image.height }
  }
  return { width: 0, height: 0 }
}

/** Match Love2D / Balatro top-left origin for grid cells (y grows downward). */
export function applyBalatroGridUV(
  tex: THREE.Texture,
  pos: GridPos,
  opts: { cellW: number; cellH: number; textureWidth: number; textureHeight: number }
): void {
  const cols = opts.textureWidth / opts.cellW
  const rows = opts.textureHeight / opts.cellH
  tex.wrapS = THREE.ClampToEdgeWrapping
  tex.wrapT = THREE.ClampToEdgeWrapping
  tex.repeat.set(1 / cols, 1 / rows)
  tex.offset.set(pos.x / cols, 1 - (pos.y + 1) / rows)
  tex.needsUpdate = true
}
