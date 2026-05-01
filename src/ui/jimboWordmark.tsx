'use client'

import React from 'react'

export interface JimboWordmarkProps {
  title: string
  subtitle?: string
  className?: string
}

/**
 * Title + subtitle hero block. Gold title, grey subtitle.
 * All styling via jimbo.css `.j-wordmark` classes.
 */
export function JimboWordmark({ title, subtitle, className = '' }: JimboWordmarkProps) {
  return (
    <div className={`j-wordmark ${className}`}>
      <div className="j-wordmark__title">{title}</div>
      {subtitle && <div className="j-wordmark__sub">{subtitle}</div>}
    </div>
  )
}
