'use client'

import React from 'react'
import { resolveJamlAssetUrl } from '../assets.js'

export const DECK_SPRITE_POS: Record<string, { x: number; y: number }> = {
  red:        { x: 0, y: 0 },
  nebula:     { x: 3, y: 0 },
  locked:     { x: 4, y: 0 },
  blue:       { x: 0, y: 2 },
  yellow:     { x: 1, y: 2 },
  green:      { x: 2, y: 2 },
  black:      { x: 3, y: 2 },
  plasma:     { x: 4, y: 2 },
  ghost:      { x: 6, y: 2 },
  magic:      { x: 0, y: 3 },
  checkered:  { x: 1, y: 3 },
  erratic:    { x: 2, y: 3 },
  abandoned:  { x: 3, y: 3 },
  painted:    { x: 4, y: 3 },
  challenge:  { x: 0, y: 4 },
  anaglyph:   { x: 2, y: 4 },
  zodiac:     { x: 3, y: 4 },
}

export const STAKE_SPRITE_POS: Record<string, { x: number; y: number }> = {
  white:  { x: 1, y: 0 },
  red:    { x: 2, y: 0 },
  green:  { x: 3, y: 0 },
  blue:   { x: 4, y: 0 },
  black:  { x: 0, y: 1 },
  purple: { x: 1, y: 1 },
  orange: { x: 2, y: 1 },
  gold:   { x: 3, y: 1 },
}

const SPRITE_WIDTH = 142
const SPRITE_HEIGHT = 190
const DECK_COLS = 7
const DECK_ROWS = 5
const STICKER_COLS = 5
const STICKER_ROWS = 3

export interface DeckSpriteProps {
  /** Deck name — case-insensitive, "Deck" suffix tolerated (e.g. "Erratic Deck" → "erratic"). */
  deck: string
  /** Optional stake name to overlay its sticker on the deck. */
  stake?: string
  /** Rendered width in pixels. Height is scaled proportionally from the 142x190 sprite. */
  size?: number
  className?: string
  style?: React.CSSProperties
}

/**
 * Balatro deck box sprite — optionally overlaid with a stake sticker.
 * Draws from the Enhancers.png atlas (deck thumbnails live in the bottom
 * two rows) and stickers.png for the stake band. Uses jaml-ui's
 * `resolveJamlAssetUrl` so consumers can override the CDN base.
 */
export function DeckSprite({ deck, stake, size = 50, className = '', style }: DeckSpriteProps) {
  const deckKey = (deck || 'erratic').toLowerCase().replace(/\s*deck$/, '').trim()
  const deckPos = DECK_SPRITE_POS[deckKey] ?? DECK_SPRITE_POS.erratic
  const stakePos = stake ? STAKE_SPRITE_POS[stake.toLowerCase().replace(/\s*stake$/, '').trim()] : null

  const scale = size / SPRITE_WIDTH
  const displayHeight = SPRITE_HEIGHT * scale

  return (
    <div
      className={className}
      style={{
        position: 'relative',
        width: size,
        height: displayHeight,
        ...style,
      }}
    >
      <div
        style={{
          position: 'absolute',
          inset: 0,
          backgroundImage: `url(${resolveJamlAssetUrl('enhancers')})`,
          backgroundSize: `${DECK_COLS * 100}% ${DECK_ROWS * 100}%`,
          backgroundPosition: `${(deckPos.x / (DECK_COLS - 1)) * 100}% ${(deckPos.y / (DECK_ROWS - 1)) * 100}%`,
          backgroundRepeat: 'no-repeat',
          imageRendering: 'pixelated',
        }}
      />
      {stakePos ? (
        <div
          style={{
            position: 'absolute',
            inset: 0,
            backgroundImage: `url(${resolveJamlAssetUrl('stickers')})`,
            backgroundSize: `${STICKER_COLS * 100}% ${STICKER_ROWS * 100}%`,
            backgroundPosition: `${(stakePos.x / (STICKER_COLS - 1)) * 100}% ${(stakePos.y / (STICKER_ROWS - 1)) * 100}%`,
            backgroundRepeat: 'no-repeat',
            imageRendering: 'pixelated',
          }}
        />
      ) : null}
    </div>
  )
}
