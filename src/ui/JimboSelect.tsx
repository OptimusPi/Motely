'use client'

import React from 'react'
import { JimboSpinner } from './JimboSpinner.js'

export interface JimboSelectOption {
  disabled?: boolean
  label?: string
  value: string
}

export interface JimboSelectProps {
  'aria-label'?: string
  disabled?: boolean
  fullWidth?: boolean
  onChange: (value: string) => void
  options: JimboSelectOption[] | string[]
  placeholder?: string
  size?: 'sm' | 'md'
  style?: React.CSSProperties
  value: string
  label?: string
}

/**
 * Select one of N values. There is no native `<select>` in the Balatro UI
 * language — options are cycled with arrows, not via an OS dropdown. So
 * JimboSelect is a `< value >` JimboSpinner that cycles through the options
 * list. If `value` isn't in `options`, we land on the first option.
 *
 * Was previously a raw `<select>` element with hand-painted gold-arrow CSS
 * gradients. That was off-brand on every axis (native control, gold control
 * affordance, no JimboButton). Rewritten to compose JimboSpinner so it
 * behaves like every other selector in the design system.
 */
export function JimboSelect({
  value,
  options,
  onChange,
  disabled = false,
  label,
  'aria-label': ariaLabel,
  style,
}: JimboSelectProps) {
  const normalized: JimboSelectOption[] = options.length === 0
    ? []
    : typeof options[0] === 'string'
      ? (options as string[]).map((v) => ({ value: v }))
      : (options as JimboSelectOption[])

  const enabled = normalized.filter((o) => !o.disabled)
  const idx = Math.max(0, enabled.findIndex((o) => o.value === value))
  const current = enabled[idx] ?? enabled[0]
  if (!current) return null

  function step(delta: number) {
    if (disabled || enabled.length === 0) return
    const next = (idx + delta + enabled.length) % enabled.length
    onChange(enabled[next].value)
  }

  return (
    <div style={style} aria-label={ariaLabel}>
      <JimboSpinner
        label={label}
        value={current.label ?? current.value}
        onPrev={() => step(-1)}
        onNext={() => step(+1)}
        canPrev={!disabled && enabled.length > 1}
        canNext={!disabled && enabled.length > 1}
      />
    </div>
  )
}
