'use client'

import React from 'react'
import { JimboColorOption, JIMBO_ANIMATIONS } from './tokens.js'

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
 * Generic adaptation of weejoker's DayNavigation — no hardcoded "Day"
 * labels, no lucide dep (inline chevron SVGs).
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
    <div
      className={className}
      style={{
        display: 'flex',
        alignItems: 'stretch',
        justifyContent: 'center',
        gap: 8,
        width: '100%',
        position: 'relative',
        ...style,
      }}
    >
      <NavButton direction="left"  onClick={onPrev} disabled={!canPrev} aria-label={prevLabel} />
      <div style={{ position: 'relative', flex: 1, minWidth: 0, display: 'flex', flexDirection: 'column' }}>{children}</div>
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
      onClick={onClick}
      disabled={disabled}
      aria-label={ariaLabel}
      title={ariaLabel}
      onMouseDown={() => !disabled && setPressed(true)}
      onMouseUp={() => setPressed(false)}
      onMouseLeave={() => setPressed(false)}
      onTouchStart={() => !disabled && setPressed(true)}
      onTouchEnd={() => setPressed(false)}
      style={{
        flexShrink: 0,
        width: 48,
        border: 'none',
        borderRadius: 8,
        cursor: disabled ? 'default' : 'pointer',
        opacity: 1,
        backgroundColor: disabled ? JimboColorOption.DARK_RED : JimboColorOption.RED,
        color: JimboColorOption.WHITE,
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'center',
        transform: pressed ? `translateY(${JIMBO_ANIMATIONS.PRESS_TRANSLATE_Y}px)` : 'translateY(0)',
        boxShadow: pressed || disabled ? 'none' : `0 ${JIMBO_ANIMATIONS.PRESS_TRANSLATE_Y}px 0 0 ${JimboColorOption.DARK_RED}`,
        transition: `transform ${JIMBO_ANIMATIONS.PRESS_DURATION}ms ease, box-shadow ${JIMBO_ANIMATIONS.PRESS_DURATION}ms ease, background-color ${JIMBO_ANIMATIONS.PRESS_DURATION}ms ease`,
      }}
    >
      <ChevronSvg direction={direction} />
    </button>
  )
}

function ChevronSvg({ direction }: { direction: 'left' | 'right' }) {
  const points = direction === 'left' ? '20,4 8,16 20,28' : '12,4 24,16 12,28'
  return (
    <svg width={32} height={32} viewBox="0 0 32 32" fill="none" stroke="currentColor" strokeWidth={3} strokeLinecap="round" strokeLinejoin="round" aria-hidden>
      <polyline points={points} />
    </svg>
  )
}
