'use client'

import React from 'react'

export interface JimboStatItem {
  value: string | number
  label: string
}

export interface JimboStatGridProps extends React.HTMLAttributes<HTMLDivElement> {
  items: JimboStatItem[]
}

/**
 * 3-column stat bar — value on top, label below.
 * All styling via jimbo.css `.j-stat-grid` classes.
 */
export function JimboStatGrid({ items, className = '', ...props }: JimboStatGridProps) {
  return (
    <div className={`j-stat-grid ${className}`} {...props}>
      {items.map((item) => (
        <div key={item.label}>
          <div className="j-stat-grid__value">{item.value}</div>
          <div className="j-stat-grid__label">{item.label}</div>
        </div>
      ))}
    </div>
  )
}
