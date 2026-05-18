'use client'

import React from 'react'
import { JimboButton } from './panel.js'
import { JimboText } from './jimboText.js'

export interface JimboStepperProps {
  value: string | number
  label?: string
  onPrev?: () => void
  onNext?: () => void
  canPrev?: boolean
  canNext?: boolean
  className?: string
}

export function JimboStepper({
  value,
  label,
  onPrev,
  onNext,
  canPrev = true,
  canNext = true,
  className = '',
}: JimboStepperProps) {
  return (
    <div className={`j-stepper-wrap ${className}`}>
      {label && (
        <div className="j-stepper__label">
          <JimboText size="sm" tone="white">{label}</JimboText>
        </div>
      )}
      <div className="j-stepper">
        <JimboButton
          tone="red"
          size="sm"
          onClick={onPrev}
          disabled={!canPrev}
          aria-label={`Previous ${label ?? 'value'}`}
        >
          {'<'}
        </JimboButton>
        <div className="j-stepper__value">
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
