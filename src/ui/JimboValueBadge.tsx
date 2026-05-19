'use client'

import React, { useEffect, useRef, useState } from 'react'
import { JimboText } from './jimboText.js'

export interface JimboValueBadgeProps {
  value: number
  onChange?: (value: number) => void
  min?: number
  max?: number
  step?: number
  unit?: string
  /** Disables click-to-edit; renders as a read-only display badge. */
  readOnly?: boolean
  className?: string
  style?: React.CSSProperties
}

/**
 * Red pill displaying a numeric value. Click to edit inline; Enter or blur
 * commits (clamped to [min, max] and snapped to `step`); Escape cancels.
 * Used as the thumb on JimboSlider and as a standalone numeric chip in panels.
 */
export function JimboValueBadge({
  value,
  onChange,
  min = 0,
  max = 100,
  step = 1,
  unit,
  readOnly = false,
  className = '',
  style,
}: JimboValueBadgeProps) {
  const [editing, setEditing] = useState(false)
  const [draft, setDraft] = useState(String(value))
  const inputRef = useRef<HTMLInputElement | null>(null)

  useEffect(() => {
    if (!editing) setDraft(String(value))
  }, [value, editing])

  useEffect(() => {
    if (editing) {
      inputRef.current?.focus()
      inputRef.current?.select()
    }
  }, [editing])

  function commit() {
    const parsed = Number(draft)
    if (Number.isFinite(parsed)) {
      const clamped = Math.min(max, Math.max(min, parsed))
      const snapped = step > 0 ? Math.round(clamped / step) * step : clamped
      onChange?.(snapped)
    }
    setEditing(false)
  }

  function cancel() {
    setDraft(String(value))
    setEditing(false)
  }

  const interactive = !readOnly && !!onChange
  const displayText = unit ? `${Math.round(value)}${unit}` : String(Math.round(value))

  if (editing) {
    return (
      <div className={`j-value-badge j-value-badge--editing ${className}`} style={style}>
        <input
          ref={inputRef}
          className="j-value-badge__input"
          type="number"
          min={min}
          max={max}
          step={step}
          value={draft}
          onChange={(e) => setDraft(e.currentTarget.value)}
          onBlur={commit}
          onKeyDown={(e) => {
            if (e.key === 'Enter') commit()
            else if (e.key === 'Escape') cancel()
          }}
        />
      </div>
    )
  }

  return (
    <button
      type="button"
      className={`j-value-badge ${interactive ? '' : 'j-value-badge--static'} ${className}`}
      style={style}
      onClick={interactive ? () => setEditing(true) : undefined}
      disabled={!interactive}
      tabIndex={interactive ? 0 : -1}
    >
      <JimboText size="xs" tone="white">{displayText}</JimboText>
    </button>
  )
}
