'use client'

import React from 'react'
import { RealStandardcard, type CardRank, type CardSuit } from './Standardcard.js'
import { JimboColorOption } from '../ui/tokens.js'
import { JimboText } from '../ui/jimboText.js'

const RANK_MAP: Record<string, CardRank> = {
  '2': '2', '3': '3', '4': '4', '5': '5', '6': '6', '7': '7', '8': '8', '9': '9', '10': '10',
  J: 'Jack', Q: 'Queen', K: 'King', A: 'Ace',
}

const SUIT_MAP: Record<string, CardSuit> = {
  H: 'Hearts', C: 'Clubs', D: 'Diamonds', S: 'Spades',
}

function parseJamlCard(input: string): { rank: CardRank; suit: CardSuit } {
  const [r, s] = input.split('_')
  return {
    rank: RANK_MAP[r] ?? '2',
    suit: SUIT_MAP[s] ?? 'Clubs',
  }
}

export interface CardFanProps {
  /** Total number of cards to render (used when `cards` is not provided). */
  count?: number
  /** Array of cards as "rank_suit" strings (e.g. ["2_C", "10_H", "A_S"]). Takes priority over `count`. */
  cards?: string[]
  /** Optional label below the fan. */
  label?: string
  showLabel?: boolean
  className?: string
  style?: React.CSSProperties
}

/**
 * Parabolic card fan with rotation + arc overlap. Ported from weejoker's
 * CardFan.tsx. Uses mathematical transforms (inline style, not Tailwind)
 * to produce the authentic Balatro spread: cards overlap toward the
 * center, tilt outward, sit higher at the edges.
 *
 * Sizing, overlap, and max rotation auto-scale with card count so full
 * 52-card decks render cleanly and tiny 3-card hands look deliberate.
 */
export function CardFan({ count = 0, cards, className = '', style, label, showLabel = true }: CardFanProps) {
  const displayCount = cards ? cards.length : count

  const cardSize =
    displayCount > 40 ? 46
      : displayCount > 30 ? 32
        : displayCount > 12 ? 36
          : displayCount > 8 ? 42
            : displayCount > 5 ? 48
              : 54

  const overlap =
    displayCount > 40 ? 0.88
      : displayCount > 30 ? 0.85
        : displayCount > 15 ? 0.75
          : displayCount > 6 ? 0.60
            : 0.45

  const cardSpacing = cardSize * (1 - overlap)

  const maxRotation =
    displayCount > 40 ? 40
      : displayCount > 20 ? 30
        : displayCount > 10 ? 15
          : 25

  const cardsHeight = 120

  return (
    <div
      className={className}
      style={{
        display: 'flex',
        flexDirection: 'column',
        alignItems: 'center',
        gap: 4,
        ...style,
      }}
    >
      <div style={{ position: 'relative', width: '100%', height: cardsHeight }}>
        {displayCount > 0 ? (
          Array.from({ length: displayCount }).map((_, i) => {
            const centerIndex = (displayCount - 1) / 2
            const offset = i - centerIndex
            const xPos = offset * cardSpacing
            // Parabolic lift — outer cards sit higher than center (bowed upward)
            const yOffset = Math.pow(Math.abs(offset / (centerIndex || 1)), 2) * (displayCount > 20 ? 20 : 10)
            const rotation = (offset / (centerIndex || 1)) * maxRotation

            const parsed = cards ? parseJamlCard(cards[i]) : { rank: '2' as CardRank, suit: 'Clubs' as CardSuit }

            return (
              <div
                key={i}
                style={{
                  position: 'absolute',
                  left: '50%',
                  bottom: 0,
                  transform: `translateX(calc(-50% + ${xPos}px)) translateY(${yOffset}px) rotate(${rotation}deg)`,
                  transformOrigin: 'bottom center',
                  zIndex: i,
                }}
              >
                <RealStandardcard
                  rank={parsed.rank}
                  suit={parsed.suit}
                  size={cardSize}
                  style={{ filter: `drop-shadow(0 2px 3px ${JimboColorOption.BLACK}66)` }}
                />
              </div>
            )
          })
        ) : (
          <div style={{
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'center',
            width: '100%',
            height: '100%',
            background: `${JimboColorOption.WHITE}0d`,
            border: `1px solid ${JimboColorOption.WHITE}0d`,
            borderRadius: 8,
          }}>
            <JimboText size="xs" uppercase tone="grey">Deck Empty</JimboText>
          </div>
        )}
      </div>

      {label && showLabel ? <JimboText size="xs" uppercase tone="grey">{label}</JimboText> : null}
    </div>
  )
}
