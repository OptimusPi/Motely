'use client'

import React from 'react'

export type JimboInfoCardTone = 'red' | 'blue' | 'green' | 'gold' | 'orange' | 'purple'

export interface JimboInfoCardProps extends React.HTMLAttributes<HTMLDivElement> {
  tone?: JimboInfoCardTone
  children: React.ReactNode
}

/**
 * Generic clickable row card — used for filter lists, seed lists, etc.
 * Border color set via tone. All styling via jimbo.css `.j-info-card`.
 */
export function JimboInfoCard({ tone, children, className = '', ...props }: JimboInfoCardProps) {
  const borderClass = tone ? `j-border--${tone}` : ''
  return (
    <div className={`j-info-card ${borderClass} ${className}`} style={tone ? { borderColor: undefined } : undefined} {...props}>
      {children}
    </div>
  )
}

/** Main body area inside an InfoCard (flex: 1, overflow-safe). */
export function JimboInfoCardBody({ children, className = '' }: { children: React.ReactNode; className?: string }) {
  return <div className={`j-info-card__body ${className}`}>{children}</div>
}

/** Title line inside an InfoCard body. */
export function JimboInfoCardTitle({ children, className = '' }: { children: React.ReactNode; className?: string }) {
  return <div className={`j-info-card__title ${className}`}>{children}</div>
}

/** Subtitle line inside an InfoCard body. */
export function JimboInfoCardSub({ children, className = '' }: { children: React.ReactNode; className?: string }) {
  return <div className={`j-info-card__sub ${className}`}>{children}</div>
}

/** Right-side aside area inside an InfoCard. */
export function JimboInfoCardAside({ children, className = '' }: { children: React.ReactNode; className?: string }) {
  return <div className={`j-info-card__aside ${className}`}>{children}</div>
}
