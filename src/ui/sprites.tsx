'use client'

import React from 'react'
import { resolveJamlAssetUrl } from '../assets.js'
import { getSpriteData, getMysterySprite, SHEET_META, type SpriteSheetType } from '../sprites/spriteMapper.js'
import { SPRITE_SHEETS } from '../sprites/spriteData.js'

export interface JimboSpriteProps {
  name: string
  sheet?: SpriteSheetType
  width?: number
  height?: number
  style?: React.CSSProperties
  className?: string
}

export function JimboSprite({ name, sheet, width = 40, height, style, className }: JimboSpriteProps) {
  const sprite = getSpriteData(name)
  const resolvedSheet: SpriteSheetType = sheet ?? sprite?.type ?? 'Jokers'
  const meta = SHEET_META[resolvedSheet]
  const mystery = getMysterySprite(resolvedSheet)
  const pos = sprite?.pos ?? mystery.pos
  
  let defaultH = width;
  if (["Jokers", "Tarots", "Vouchers", "Boosters", "Decks", "Enhancers", "Editions"].includes(resolvedSheet)) {
    defaultH = Math.round((width * 95) / 71);
  }
  const h = height ?? defaultH;

  if (!meta) return null

  const bgW = width * meta.cols
  const bgH = h * meta.rows
  const bgX = -(pos.x * width)
  const bgY = -(pos.y * h)

  return (
    <div className={className} style={{
      width, height: h, flexShrink: 0,
      backgroundImage: `url(${resolveJamlAssetUrl(meta.assetKey)})`,
      backgroundSize: `${bgW}px ${bgH}px`,
      backgroundPosition: `${bgX}px ${bgY}px`,
      backgroundRepeat: 'no-repeat',
      imageRendering: 'pixelated',
      ...style,
    }} />
  )
}

export interface StakeSpriteProps {
  stake: string
  width?: number
  height?: number
  style?: React.CSSProperties
}

const STAKE_MAP: string[] = ["White", "Red", "Green", "Black", "Blue", "Purple", "Orange", "Gold"]

export function StakeSprite({ stake, width = 29, height, style }: StakeSpriteProps) {
  const index = STAKE_MAP.indexOf(stake.replace(" Stake", ""))
  const idx = index >= 0 ? index : 0
  const x = idx % 5
  const y = Math.floor(idx / 5)
  const h = height ?? width
  const bgW = width * 5
  const bgH = h * 2

  return (
    <div style={{
      width, height: h, flexShrink: 0,
      backgroundImage: `url(${resolveJamlAssetUrl('stakes')})`,
      backgroundSize: `${bgW}px ${bgH}px`,
      backgroundPosition: `-${x * width}px -${y * h}px`,
      backgroundRepeat: 'no-repeat',
      imageRendering: 'pixelated',
      ...style,
    }} />
  )
}

export interface DeckSpriteProps {
  deck: string
  width?: number
  height?: number
  style?: React.CSSProperties
}

// Deck-back sprites live in Enhancers.png (the same sheet that holds card
// enhancement backgrounds + seals). Map deck names to their (col, row) cell
// on that sheet. Names that aren't mapped fall back to Red.
//
// The 8BitDeck.png sheet (registered as `deck`) is 13×4 = 52 card FACES
// only — no deck-backs there. Earlier code sampled column 12 of deck and
// happened to render Aces; the visible "Red Deck" in RunConfigModal works
// because it pulls from enhancers (0,0), not from deck.
const DECK_TILE: Record<string, { x: number; y: number }> = {
  Red:     { x: 0, y: 0 },
  Blue:    { x: 0, y: 2 },
  Yellow:  { x: 1, y: 2 },
  Green:   { x: 2, y: 2 },
  Black:   { x: 3, y: 2 },
  Magic:   { x: 4, y: 2 },
}

export function DeckSprite({ deck, width = 71, height, style }: DeckSpriteProps) {
  const baseDeck = deck.replace(" Deck", "")
  const tile = DECK_TILE[baseDeck] ?? DECK_TILE.Red
  const h = height ?? (width * 95 / 71)
  const sheet = SPRITE_SHEETS.enhancers

  return (
    <div style={{
      width, height: h, flexShrink: 0,
      backgroundImage: `url(${resolveJamlAssetUrl(sheet.asset)})`,
      backgroundSize: `${width * sheet.columns}px ${h * sheet.rows}px`,
      backgroundPosition: `${-tile.x * width}px ${-tile.y * h}px`,
      backgroundRepeat: 'no-repeat',
      imageRendering: 'pixelated',
      ...style,
    }} />
  )
}
