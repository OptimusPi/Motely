'use client'

import React from 'react'

export type JimboStatTone = 'red' | 'blue' | 'green' | 'orange' | 'gold' | 'purple' | 'grey'
export type JimboStatValueTone = 'gold' | 'red' | 'blue'

export interface JimboStatCalloutProps {
  label: React.ReactNode
  value: React.ReactNode
  tone?: JimboStatTone
  valueTone?: JimboStatValueTone
  className?: string
  style?: React.CSSProperties
}

/**
 * HUD primitive: colored label strip stacked above a big pixel number.
 * The Hands / Discards / $ / Ante / Round panels in the Balatro game UI.
 */
export function JimboStatCallout({
  label,
  value,
  tone = 'red',
  valueTone,
  className = '',
  style,
}: JimboStatCalloutProps) {
  const valueClass = valueTone ? `j-stat__value--${valueTone}` : ''
  return (
    <div className={`j-stat ${className}`.trim()} style={style}>
      <div className={`j-stat__label j-stat__label--${tone}`}>{label}</div>
      <div className={`j-stat__value ${valueClass}`.trim()}>{value}</div>
    </div>
  )
}

export type JimboStatus = 'idle' | 'running' | 'ok' | 'error' | 'paused'

export interface JimboStatusPillProps {
  status: JimboStatus
  label?: React.ReactNode
  className?: string
}

export function JimboStatusPill({ status, label, className = '' }: JimboStatusPillProps) {
  return (
    <span className={`j-status-pill j-status-pill--${status} ${className}`.trim()}>
      <span className="j-status-pill__dot" aria-hidden />
      {label ?? status}
    </span>
  )
}

export interface JimboPriceTagProps {
  amount: number
  float?: boolean
  className?: string
  style?: React.CSSProperties
}

/** Yellow `$X` tag. Pass `float` to absolutely position above an item. */
export function JimboPriceTag({ amount, float = false, className = '', style }: JimboPriceTagProps) {
  return (
    <span
      className={`j-price-tag ${float ? 'j-price-tag--float' : ''} ${className}`.trim()}
      style={style}
    >
      ${amount}
    </span>
  )
}

export interface JimboSidewaysLabelProps {
  children: React.ReactNode
  className?: string
  style?: React.CSSProperties
}

/** Vertical all-caps pixel text. For panel-edge labels. */
export function JimboSidewaysLabel({ children, className = '', style }: JimboSidewaysLabelProps) {
  return (
    <div className={`j-sideways-label ${className}`.trim()} style={style}>
      {children}
    </div>
  )
}
