'use client'

import React from 'react'

export type JimboSectionTone = 'red' | 'orange' | 'blue' | 'green' | 'purple' | 'gold'

export interface JimboSectionHeaderProps {
  title: React.ReactNode
  tone?: JimboSectionTone
  className?: string
  style?: React.CSSProperties
}

/**
 * Colored strip across the top of a section + downward triangle tab below.
 * Marks the active page of a multi-tab area.
 */
export function JimboSectionHeader({
  title,
  tone = 'red',
  className = '',
  style,
}: JimboSectionHeaderProps) {
  return (
    <div className={`j-section-header ${className}`.trim()} style={style}>
      <div className={`j-section-header__bar j-section-header__bar--${tone}`}>{title}</div>
      <div className={`j-section-header__triangle j-section-header__triangle--${tone}`} />
    </div>
  )
}

export interface JimboMarqueeProps {
  title: React.ReactNode
  subtitle?: React.ReactNode
  className?: string
  style?: React.CSSProperties
}

/** The SHOP sign — red panel + dotted-bulb border + gold pixel headline. */
export function JimboMarquee({ title, subtitle, className = '', style }: JimboMarqueeProps) {
  return (
    <div className={`j-marquee ${className}`.trim()} style={style}>
      <div className="j-marquee__bulbs" aria-hidden />
      <div className="j-marquee__title">{title}</div>
      {subtitle && <div className="j-marquee__sub">{subtitle}</div>}
    </div>
  )
}
