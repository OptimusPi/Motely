import React from 'react'

export type JimboBadgeTone = 'dark' | 'blue' | 'red' | 'green' | 'gold' | 'grey' | 'orange' | 'purple'

export interface JimboBadgeProps {
  size?: 'sm' | 'md'
  tone?: JimboBadgeTone
  children: React.ReactNode
  className?: string
}

/**
 * Small colored label pill. Matches Balatro's in-game tag/rarity badges.
 * All styling via jimbo.css `.j-badge` classes.
 */
export function JimboBadge({ size = 'sm', tone = 'dark', className, children }: JimboBadgeProps) {
  return (
    <span className={`j-badge j-badge--${size} j-badge--${tone} ${className ?? ''}`}>
      {children}
    </span>
  )
}
