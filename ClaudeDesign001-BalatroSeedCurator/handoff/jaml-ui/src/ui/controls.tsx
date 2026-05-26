'use client'

import React from 'react'
import { JimboText } from './jimboText.js'

export type JimboToggleTone = 'blue' | 'green' | 'red' | 'orange'

export interface JimboToggleProps {
  checked: boolean
  onChange?: (next: boolean) => void
  tone?: JimboToggleTone
  className?: string
  'aria-label'?: string
}

export function JimboToggle({
  checked,
  onChange,
  tone = 'blue',
  className = '',
  'aria-label': ariaLabel,
}: JimboToggleProps) {
  return (
    <button
      type="button"
      role="checkbox"
      aria-checked={checked}
      aria-label={ariaLabel}
      data-on={checked}
      className={`j-toggle j-toggle--${tone} ${className}`.trim()}
      onClick={() => onChange?.(!checked)}
    />
  )
}

export interface JimboToggleRowProps {
  label: React.ReactNode
  checked: boolean
  onChange?: (next: boolean) => void
  tone?: JimboToggleTone
}

export function JimboToggleRow({ label, checked, onChange, tone = 'blue' }: JimboToggleRowProps) {
  return (
    <label className="j-toggle-row" onClick={(e) => e.preventDefault()}>
      <JimboToggle
        checked={checked}
        onChange={onChange}
        tone={tone}
        aria-label={typeof label === 'string' ? label : undefined}
      />
      <JimboText size="sm" tone="white">{label}</JimboText>
    </label>
  )
}
