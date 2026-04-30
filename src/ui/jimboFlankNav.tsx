'use client'

import React from 'react'
import { JIMBO_ANIMATIONS } from './tokens.js'

export interface JimboFlankNavProps {
  onPrev: () => void
  onNext: () => void
  canPrev?: boolean
  canNext?: boolean
  prevLabel?: string
  nextLabel?: string
  children: React.ReactNode
  className?: string
  style?: React.CSSProperties
}

/**
 * Prev/next navigation with flanking buttons around a central stage.
 * No hardcoded labels, no lucide dep (inline chevron SVGs).
 */
export function JimboFlankNav({
  onPrev,
  onNext,
  canPrev = true,
  canNext = true,
  prevLabel = 'Previous',
  nextLabel = 'Next',
  children,
  className = '',
  style,
}: JimboFlankNavProps) {
  return (
    <div className={`j-flank ${className}`} style={style}>
      <NavButton direction="left"  onClick={onPrev} disabled={!canPrev} aria-label={prevLabel} />
      <div className="j-flank__content">{children}</div>
      <NavButton direction="right" onClick={onNext} disabled={!canNext} aria-label={nextLabel} />
    </div>
  )
}

function NavButton({
  direction,
  onClick,
  disabled,
  'aria-label': ariaLabel,
}: {
  direction: 'left' | 'right'
  onClick: () => void
  disabled: boolean
  'aria-label': string
}) {
  const [pressed, setPressed] = React.useState(false)
  return (
    <button
      type="button"
      className="j-flank__btn"
      data-pressed={pressed && !disabled}
      onClick={onClick}
      disabled={disabled}
      aria-label={ariaLabel}
      title={ariaLabel}
      onMouseDown={() => !disabled && setPressed(true)}
      onMouseUp={() => setPressed(false)}
      onMouseLeave={() => setPressed(false)}
      onTouchStart={() => !disabled && setPressed(true)}
      onTouchEnd={() => setPressed(false)}
    >
      <ChevronSvg direction={direction} />
    </button>
  )
}

function ChevronSvg({ direction }: { direction: 'left' | 'right' }) {
  const points = direction === 'left' ? '18,4 8,14 18,24' : '10,4 20,14 10,24'
  return (
    <svg width={28} height={28} viewBox="0 0 28 28" fill="none" stroke="currentColor" strokeWidth={2} strokeLinecap="round" strokeLinejoin="round" aria-hidden>
      <polyline points={points} />
    </svg>
  )
}
