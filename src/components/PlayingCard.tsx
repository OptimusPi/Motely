'use client'

import React from 'react'
import { resolveJamlAssetUrl } from '../assets.js'
import { RANK_MAP, SUIT_MAP, ENHANCER_MAP, SEAL_MAP, EDITION_MAP } from '../sprites/spriteData.js'
function cn(...classes: (string | undefined | false)[]) { return classes.filter(Boolean).join(" "); }

export type CardSuit = 'Hearts' | 'Diamonds' | 'Clubs' | 'Spades' | 'hearts' | 'diamonds' | 'clubs' | 'spades'
export type CardRank = 'Ace' | 'King' | 'Queen' | 'Jack' | '10' | '9' | '8' | '7' | '6' | '5' | '4' | '3' | '2' | 'A' | 'K' | 'Q' | 'J'
export type CardEnhancement = 'bonus' | 'mult' | 'wild' | 'glass' | 'steel' | 'stone' | 'gold' | 'lucky' | null
export type CardSeal = 'gold' | 'red' | 'blue' | 'purple' | null
export type CardEdition = 'Foil' | 'Holographic' | 'Polychrome' | 'Negative' | null

const CARD_WIDTH = 71
const CARD_HEIGHT = 95

const RANK_ALIAS: Record<string, string> = { A: 'Ace', K: 'King', Q: 'Queen', J: 'Jack' }
const pascal = (s: string) => s[0].toUpperCase() + s.slice(1).toLowerCase()

interface RealPlayingCardProps {
    suit: CardSuit
    rank: CardRank
    enhancement?: CardEnhancement
    seal?: CardSeal
    edition?: CardEdition
    className?: string
    size?: number
    style?: React.CSSProperties
}

export function RealPlayingCard({
    suit,
    rank,
    enhancement,
    seal,
    edition,
    className,
    size = 71,
    style
}: RealPlayingCardProps) {
    const rankKey = RANK_ALIAS[rank] ?? rank
    const suitKey = pascal(suit)
    const col = RANK_MAP[rankKey]
    const row = SUIT_MAP[suitKey]

    if (col === undefined || row === undefined) {
        console.warn(`Invalid card: ${rank} of ${suit}`)
        return null
    }

    const scale = size / CARD_WIDTH
    const finalH = size * (CARD_HEIGHT / CARD_WIDTH)

    // Base card position
    const bgX = -col * CARD_WIDTH
    const bgY = -row * CARD_HEIGHT

    // Enhancement background (if any) — ENHANCER_MAP is PascalCase, prop is lowercase
    const enhPos = enhancement ? ENHANCER_MAP[pascal(enhancement)] ?? { x: 0, y: 0 } : { x: 0, y: 0 }
    const enhBgX = -enhPos.x * CARD_WIDTH
    const enhBgY = -enhPos.y * CARD_HEIGHT

    // Seal overlay — SEAL_MAP is keyed by "Gold"/"Red"/"Blue"/"Purple"
    const sealPos = seal ? SEAL_MAP[pascal(seal)] ?? null : null
    const sealBgX = sealPos ? -sealPos.x * CARD_WIDTH : 0
    const sealBgY = sealPos ? -sealPos.y * CARD_HEIGHT : 0

    // Edition overlay — EDITION_MAP gives column index on 5-wide editions sheet
    const editionCol = edition ? EDITION_MAP[edition] : undefined
    const editionBgX = editionCol !== undefined ? -editionCol * CARD_WIDTH : 0
    const editionBgY = 0

    const isNegative = edition === 'Negative'
    const baseFilter = isNegative ? 'invert(0.94)' : 'none'
    const enhancersUrl = resolveJamlAssetUrl('enhancers')
    const deckUrl = resolveJamlAssetUrl('deck')
    const editionsUrl = resolveJamlAssetUrl('editions')

    return (
        <div
            className={cn('relative overflow-hidden inline-block select-none', className)}
            style={{
                width: size,
                height: finalH,
                imageRendering: 'pixelated',
                ...style
            }}
            title={`${rank} of ${suit}${enhancement ? ` (${enhancement})` : ''}${seal ? ` [${seal} seal]` : ''}${edition ? ` {${edition}}` : ''}`}
        >
            {/* Enhancement background layer */}
            <div
                className="absolute inset-0"
                style={{
                    backgroundImage: `url(${enhancersUrl})`,
                    backgroundPosition: `${enhBgX}px ${enhBgY}px`,
                    width: CARD_WIDTH,
                    height: CARD_HEIGHT,
                    transform: `scale(${scale})`,
                    transformOrigin: 'top left',
                    backgroundRepeat: 'no-repeat',
                }}
            />

            {/* Card face layer */}
            <div
                className="absolute inset-0 z-[1]"
                style={{
                    backgroundImage: `url(${deckUrl})`,
                    backgroundPosition: `${bgX}px ${bgY}px`,
                    width: CARD_WIDTH,
                    height: CARD_HEIGHT,
                    transform: `scale(${scale})`,
                    transformOrigin: 'top left',
                    backgroundRepeat: 'no-repeat',
                    filter: baseFilter
                }}
            />

            {/* Edition overlay */}
            {edition && edition !== 'Negative' && (
                <div
                    className="absolute inset-0 z-[2] mix-blend-screen opacity-60"
                    style={{
                        backgroundImage: `url(${editionsUrl})`,
                        backgroundPosition: `${editionBgX}px ${editionBgY}px`,
                        width: CARD_WIDTH,
                        height: CARD_HEIGHT,
                        transform: `scale(${scale})`,
                        transformOrigin: 'top left',
                        backgroundRepeat: 'no-repeat',
                    }}
                />
            )}

            {/* Seal overlay */}
            {seal && (
                <div
                    className="absolute inset-0 z-[3]"
                    style={{
                        backgroundImage: `url(${enhancersUrl})`,
                        backgroundPosition: `${sealBgX}px ${sealBgY}px`,
                        width: CARD_WIDTH,
                        height: CARD_HEIGHT,
                        transform: `scale(${scale})`,
                        transformOrigin: 'top left',
                        backgroundRepeat: 'no-repeat',
                    }}
                />
            )}

            {/* Negative tint */}
            {isNegative && (
                <div className="absolute inset-0 z-[4] bg-red-500/10 pointer-events-none mix-blend-overlay" />
            )}
        </div>
    )
}
