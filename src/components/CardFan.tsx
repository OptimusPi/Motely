'use client'

import React from 'react'
import { StandardCard, type CardRank, type CardSuit } from './StandardCard.js'
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
// Fan width budget — the horizontal space the spread is allowed to consume.
// Cards never cross this; rotation+lift may extend a few px outside.
const FAN_WIDTH = 240
// Card sizing — tapers from BIG (small hand) to SMALL (full deck) linearly.
const CARD_W_MAX = 54
const CARD_W_MIN = 36
// Max angular spread of the whole fan, in degrees (outermost-to-outermost).
const FAN_ARC_MAX = 60
// Max vertical lift of the outermost cards above the centerline.
const LIFT_MAX = 22

export function CardFan({ count = 0, cards, className = '', style, label, showLabel = true }: CardFanProps) {
  const n = cards ? cards.length : count

  // Card width tapers smoothly; for n ≤ 5 stays at max, then linearly down to min by n ≈ 41.
  const cardW = Math.max(CARD_W_MIN, CARD_W_MAX - Math.max(0, n - 5) * 0.5)

  // Step between adjacent card centers. With 1 card, step is unused.
  // With many cards, step shrinks so the whole fan fits FAN_WIDTH.
  // Capped at near-edge-to-edge so small hands don't spread out absurdly.
  const maxStep = cardW * 0.9
  const step = n > 1 ? Math.min(maxStep, (FAN_WIDTH - cardW) / (n - 1)) : 0

  // Arc grows linearly with count, capped. 1 card → no rotation.
  const halfArc = Math.min(FAN_ARC_MAX, Math.max(0, n - 1) * 3) / 2

  // Outer-card lift compensates for rotation tilting them down past the centerline.
  const lift = Math.min(LIFT_MAX, n * 0.6)

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
        {n > 0 ? (
          Array.from({ length: n }).map((_, i) => {
            const centerIndex = (n - 1) / 2
            const offset = i - centerIndex            // -k … 0 … +k
            const t = centerIndex > 0 ? offset / centerIndex : 0  // -1 … 0 … +1

            const x = offset * step
            // Circular-arc lift instead of t² parabola. Parabola was shallow
            // through the middle and only curled at the tips; cosine of the
            // rotation angle gives a uniform smile that bows evenly across the
            // whole fan. `lift` is the depth (max dip at the centre).
            const angleRad = (t * halfArc) * Math.PI / 180
            const y = (1 - Math.cos(angleRad)) * -lift / (1 - Math.cos((halfArc) * Math.PI / 180) || 1)
            const rot = t * halfArc

            const parsed = cards ? parseJamlCard(cards[i]) : { rank: '2' as CardRank, suit: 'Clubs' as CardSuit }

            return (
              <div
                key={i}
                style={{
                  position: 'absolute',
                  left: '50%',
                  bottom: 0,
                  transform: `translateX(calc(-50% + ${x}px)) translateY(${y}px) rotate(${rot}deg)`,
                  transformOrigin: 'bottom center',
                  zIndex: i,
                }}
              >
                <StandardCard
                  rank={parsed.rank}
                  suit={parsed.suit}
                  size={cardW}
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
            <JimboText size="xs" tone="grey">Deck Empty</JimboText>
          </div>
        )}
      </div>

      {label && showLabel ? <JimboText size="xs" tone="grey">{label}</JimboText> : null}
    </div>
  )
}
