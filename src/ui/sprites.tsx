'use client'

import React from 'react'
import { resolveJamlAssetUrl } from '../assets.js'
import { getSpriteData, getMysterySprite, SHEET_META, type SpriteSheetType } from '../sprites/spriteMapper.js'

export interface JimboSpriteProps {
  name: string
  sheet?: SpriteSheetType
  width?: number
  height?: number
  style?: React.CSSProperties
}

export function JimboSprite({ name, sheet, width = 40, height, style }: JimboSpriteProps) {
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
    <div style={{
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

const DECK_ROWS: Record<string, number> = {
  Red: 0,
  Blue: 1,
  Yellow: 2,
  Green: 3,
  Black: 0,
  Magic: 1,
  Nebula: 2,
  Ghost: 3,
}

export function DeckSprite({ deck, width = 71, height, style }: DeckSpriteProps) {
  const baseDeck = deck.replace(" Deck", "")
  const y = DECK_ROWS[baseDeck] ?? 0
  const x = 12
  const h = height ?? (width * 95 / 71)
  const bgW = width * 13
  const bgH = h * 4

  return (
    <div style={{
      width, height: h, flexShrink: 0,
      backgroundImage: `url(${resolveJamlAssetUrl('deck')})`,
      backgroundSize: `${bgW}px ${bgH}px`,
      backgroundPosition: `-${x * width}px -${y * h}px`,
      backgroundRepeat: 'no-repeat',
      imageRendering: 'pixelated',
      ...style,
    }} />
  )
}
