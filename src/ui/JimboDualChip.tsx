'use client'

import React from 'react'
import { JimboText } from './jimboText.js'

export type JimboDualChipTone = 'blue' | 'red' | 'green' | 'gold' | 'dark'

export interface JimboDualChipHalf {
  value: React.ReactNode
  tone: JimboDualChipTone
  label?: string
}

export interface JimboDualChipProps {
  left: JimboDualChipHalf
  right: JimboDualChipHalf
  className?: string
}

/**
 * Two stat halves joined into one pill. Used for the `0 × 0` Hands × Discards
 * chip on Balatro's run-info side panel.
 */
export function JimboDualChip({ left, right, className = '' }: JimboDualChipProps) {
  return (
    <div className={`j-dual-chip ${className}`}>
      <div className={`j-dual-chip__half j-dual-chip__half--left j-dual-chip__half--${left.tone}`}>
        <JimboText size="md" tone={left.tone === 'gold' ? 'default' : 'white'}>{left.value}</JimboText>
      </div>
      <div className={`j-dual-chip__half j-dual-chip__half--${right.tone}`}>
        <JimboText size="md" tone={right.tone === 'gold' ? 'default' : 'white'}>{right.value}</JimboText>
      </div>
    </div>
  )
}
