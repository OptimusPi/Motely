'use client'

import React from 'react'
import { resolveJamlAssetUrl } from '../assets.js'
import { RANK_MAP, SUIT_MAP, ENHANCER_MAP, SEAL_MAP, EDITION_MAP } from '../sprites/spriteData.js'

// ── Enums ──────────────────────────────────────────────────────────────────
// Single canonical form per value. No case tolerance, no abbreviations.
// Value strings match the keys used by RANK_MAP / SUIT_MAP / ENHANCER_MAP / SEAL_MAP / EDITION_MAP
// so no aliasing/normalization is needed at render time.

export const CardSuit = {
  Hearts: 'Hearts',
  Diamonds: 'Diamonds',
  Clubs: 'Clubs',
  Spades: 'Spades',
} as const
export type CardSuit = typeof CardSuit[keyof typeof CardSuit]

export const CardRank = {
  Two: '2',
  Three: '3',
  Four: '4',
  Five: '5',
  Six: '6',
  Seven: '7',
  Eight: '8',
  Nine: '9',
  Ten: '10',
  Jack: 'Jack',
  Queen: 'Queen',
  King: 'King',
  Ace: 'Ace',
} as const
export type CardRank = typeof CardRank[keyof typeof CardRank]

export const CardEnhancement = {
  Bonus: 'Bonus',
  Mult: 'Mult',
  Wild: 'Wild',
  Glass: 'Glass',
  Steel: 'Steel',
  Stone: 'Stone',
  Gold: 'Gold',
  Lucky: 'Lucky',
} as const
export type CardEnhancement = typeof CardEnhancement[keyof typeof CardEnhancement]

export const CardSeal = {
  Gold: 'Gold',
  Red: 'Red',
  Blue: 'Blue',
  Purple: 'Purple',
} as const
export type CardSeal = typeof CardSeal[keyof typeof CardSeal]

export const CardEdition = {
  Foil: 'Foil',
  Holographic: 'Holographic',
  Polychrome: 'Polychrome',
  Negative: 'Negative',
} as const
export type CardEdition = typeof CardEdition[keyof typeof CardEdition]

// ── Sprite geometry ────────────────────────────────────────────────────────

const CARD_WIDTH = 71
const CARD_HEIGHT = 95

interface StandardCardProps {
  suit: CardSuit
  rank: CardRank
  enhancement?: CardEnhancement
  seal?: CardSeal
  edition?: CardEdition
  className?: string
  size?: number
  style?: React.CSSProperties
}

export function StandardCard({
  suit,
  rank,
  enhancement,
  seal,
  edition,
  className,
  size = 71,
  style,
}: StandardCardProps) {
  const col = RANK_MAP[rank]
  const row = SUIT_MAP[suit]

  if (col === undefined || row === undefined) {
    console.warn(`Invalid card: ${rank} of ${suit}`)
    return null
  }

  const scale = size / CARD_WIDTH
  const finalH = size * (CARD_HEIGHT / CARD_WIDTH)

  const bgX = -col * CARD_WIDTH
  const bgY = -row * CARD_HEIGHT

  const enhPos = enhancement ? ENHANCER_MAP[enhancement] ?? null : null
  const sealPos = seal ? SEAL_MAP[seal] ?? null : null
  const editionCol = edition ? EDITION_MAP[edition] : undefined

  const isNegative = edition === CardEdition.Negative
  const baseFilter = isNegative ? 'invert(0.94)' : 'none'

  const enhancersUrl = resolveJamlAssetUrl('enhancers')
  const deckUrl = resolveJamlAssetUrl('deck')
  const editionsUrl = resolveJamlAssetUrl('editions')

  const layerBase: React.CSSProperties = {
    position: 'absolute',
    top: 0,
    left: 0,
    width: CARD_WIDTH,
    height: CARD_HEIGHT,
    transform: `scale(${scale})`,
    transformOrigin: 'top left',
    backgroundRepeat: 'no-repeat',
  }

  return (
    <div
      className={className}
      style={{
        position: 'relative',
        display: 'inline-block',
        overflow: 'hidden',
        userSelect: 'none',
        width: size,
        height: finalH,
        imageRendering: 'pixelated',
        ...style,
      }}
      title={`${rank} of ${suit}${enhancement ? ` (${enhancement})` : ''}${seal ? ` [${seal} seal]` : ''}${edition ? ` {${edition}}` : ''}`}
    >
      {/* Enhancement background (skipped when none — was previously always cell 0,0) */}
      {enhPos && (
        <div
          style={{
            ...layerBase,
            zIndex: 0,
            backgroundImage: `url(${enhancersUrl})`,
            backgroundPosition: `${-enhPos.x * CARD_WIDTH}px ${-enhPos.y * CARD_HEIGHT}px`,
          }}
        />
      )}

      {/* Card face */}
      <div
        style={{
          ...layerBase,
          zIndex: 1,
          backgroundImage: `url(${deckUrl})`,
          backgroundPosition: `${bgX}px ${bgY}px`,
          filter: baseFilter,
        }}
      />

      {/* Edition overlay */}
      {edition && edition !== CardEdition.Negative && editionCol !== undefined && (
        <div
          style={{
            ...layerBase,
            zIndex: 2,
            mixBlendMode: 'screen',
            opacity: 0.6,
            backgroundImage: `url(${editionsUrl})`,
            backgroundPosition: `${-editionCol * CARD_WIDTH}px 0px`,
          }}
        />
      )}

      {/* Seal overlay */}
      {sealPos && (
        <div
          style={{
            ...layerBase,
            zIndex: 3,
            backgroundImage: `url(${enhancersUrl})`,
            backgroundPosition: `${-sealPos.x * CARD_WIDTH}px ${-sealPos.y * CARD_HEIGHT}px`,
          }}
        />
      )}

      {/* Negative tint */}
      {isNegative && (
        <div
          style={{
            position: 'absolute',
            inset: 0,
            zIndex: 4,
            background: 'rgba(239, 68, 68, 0.1)',
            mixBlendMode: 'overlay',
            pointerEvents: 'none',
          }}
        />
      )}
    </div>
  )
}
