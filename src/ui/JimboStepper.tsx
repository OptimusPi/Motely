'use client'

import React from 'react'

export interface JimboStepperProps {
  /** Total number of pages/steps. */
  count: number
  /** Current page index (0-based). */
  index: number
  /** Optional click handler to jump to a specific page. */
  onIndexChange?: (i: number) => void
  /** Label for screen readers describing what the dots track. */
  ariaLabel?: string
  className?: string
  style?: React.CSSProperties
}

/**
 * Page-dot indicator. A row of small circles: the active page is a filled
 * white dot; the others are dim grey dots. Matches Balatro's carousel page
 * indicator pattern (deck picker, blind preview, etc.).
 *
 * For the value spinner pattern (`< OFF >`) see JimboSpinner — different
 * component, despite the previous misnomer.
 */
export function JimboStepper({
  count,
  index,
  onIndexChange,
  ariaLabel = 'Page indicator',
  className = '',
  style,
}: JimboStepperProps) {
  if (count <= 1) return null
  const interactive = !!onIndexChange

  return (
    <div
      className={`j-stepper ${className}`}
      role="tablist"
      aria-label={ariaLabel}
      style={style}
    >
      {Array.from({ length: count }).map((_, i) => {
        const active = i === index
        const dot = (
          <span
            className="j-stepper__dot"
            data-active={active}
            aria-hidden
          />
        )
        if (!interactive) {
          return (
            <span key={i} role="tab" aria-selected={active}>
              {dot}
            </span>
          )
        }
        return (
          <button
            key={i}
            type="button"
            className="j-stepper__hit"
            role="tab"
            aria-selected={active}
            aria-label={`Page ${i + 1} of ${count}`}
            onClick={() => onIndexChange?.(i)}
          >
            {dot}
          </button>
        )
      })}
    </div>
  )
}
