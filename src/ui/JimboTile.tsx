'use client'

import React from 'react'
import { resolveJamlAssetUrl } from '../assets.js'
import { SPRITE_SHEETS } from '../sprites/spriteData.js'

export type JimboTileSheet = keyof typeof SPRITE_SHEETS

export interface JimboTileProps {
  /** Sprite sheet to sample from. Must be registered in SPRITE_SHEETS. */
  sheet: JimboTileSheet
  /** Column index of the tile within the sheet (0-based). */
  x: number
  /** Row index of the tile within the sheet (0-based). */
  y: number
  /** Rendered tile width in CSS pixels. */
  width?: number
  /** Rendered tile height. Defaults to width × natural-cell-aspect for card sheets, otherwise width. */
  height?: number
  className?: string
  style?: React.CSSProperties
  title?: string
}

// Sheets whose native cells are 71×95 (card aspect) — anything else is
// treated as square.
const CARD_ASPECT_SHEETS: ReadonlySet<JimboTileSheet> = new Set([
  'deck', 'enhancers', 'editions',
])

/**
 * Lowest-level Jimbo sprite primitive: render a single cell of a registered
 * sprite sheet by its (x, y) grid coordinates. No name lookup, no metadata —
 * just `<JimboTile sheet="enhancers" x={0} y={0} width={56} />`.
 *
 * Use this when you have raw coordinates. For named items (jokers, tarots,
 * etc.) prefer JimboSprite which does the name-to-position lookup.
 */
export function JimboTile({
  sheet, x, y, width = 40, height, className, style, title,
}: JimboTileProps) {
  const meta = SPRITE_SHEETS[sheet]
  if (!meta) return null

  const isCardAspect = CARD_ASPECT_SHEETS.has(sheet)
  const cellH = height ?? (isCardAspect ? Math.round((width * 95) / 71) : width)

  return (
    <div
      className={className}
      title={title}
      style={{
        width,
        height: cellH,
        flexShrink: 0,
        backgroundImage: `url(${resolveJamlAssetUrl(meta.asset)})`,
        backgroundSize: `${width * meta.columns}px ${cellH * meta.rows}px`,
        backgroundPosition: `${-x * width}px ${-y * cellH}px`,
        backgroundRepeat: 'no-repeat',
        imageRendering: 'pixelated',
        ...style,
      }}
    />
  )
}
