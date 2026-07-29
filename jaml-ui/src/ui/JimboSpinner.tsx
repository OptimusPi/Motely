'use client'

import React from 'react'
import { JimboButton } from './panel.js'
import { JimboText } from './jimboText.js'

export interface JimboSpinnerProps {
  value: string | number
  label?: string
  onPrev?: () => void
  onNext?: () => void
  canPrev?: boolean
  canNext?: boolean
  className?: string
}

/**
 * Two-state-or-more value spinner: red `<` button + value chip + red `>` button.
 * This is the canonical Balatro on/off control (IMG_3678: `Shadows < OFF >`)
 * AND the canonical option-cycler (deck/stake/aesthetic picker).
 *
 * Note: previously named JimboStepper — that was a misnomer. A stepper is a
 * page-dot indicator (see JimboStepper). This is a spinner.
 */
export function JimboSpinner({
  value,
  label,
  onPrev,
  onNext,
  canPrev = true,
  canNext = true,
  className = '',
}: JimboSpinnerProps) {
  return (
    <div className={`j-spinner-wrap ${className}`}>
      {label && (
        <div className="j-spinner__label">
          <JimboText size="sm" tone="white">{label}</JimboText>
        </div>
      )}
      <div className="j-spinner">
        <JimboButton
          tone="red"
          size="sm"
          onClick={onPrev}
          disabled={!canPrev}
          aria-label={`Previous ${label ?? 'value'}`}
        >
          {'<'}
        </JimboButton>
        <div className="j-spinner__value">
          <JimboText size="sm" tone="white">{value}</JimboText>
        </div>
        <JimboButton
          tone="red"
          size="sm"
          onClick={onNext}
          disabled={!canNext}
          aria-label={`Next ${label ?? 'value'}`}
        >
          {'>'}
        </JimboButton>
      </div>
    </div>
  )
}
