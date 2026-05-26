'use client'

import React from 'react'

export type JimboSectionTone = 'red' | 'blue' | 'green' | 'gold' | 'orange' | 'purple'

export interface JimboSectionHeaderProps {
  label: string
  tone?: JimboSectionTone
  className?: string
}

/**
 * Colored tag + horizontal rule — reusable section divider.
 * All styling via jimbo.css `.j-section-header` classes + tone utilities.
 */
export function JimboSectionHeader({ label, tone = 'blue', className = '' }: JimboSectionHeaderProps) {
  return (
    <div className={`j-section-header ${className}`}>
      <div className={`j-section-header__tag j-bg--${tone}`}>{label}</div>
      <div className={`j-section-header__rule j-bg--${tone}`} />
    </div>
  )
}
