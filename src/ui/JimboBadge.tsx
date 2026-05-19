import React from 'react'

// No 'gold' tone — Balatro doesn't use gold badges. Gold is reserved for
// text-on-dark (prices, titles), not for chip/pill surfaces.
export type JimboBadgeTone = 'dark' | 'blue' | 'red' | 'green' | 'grey' | 'orange' | 'purple'

export interface JimboBadgeProps extends React.HTMLAttributes<HTMLSpanElement> {
  size?: 'sm' | 'md'
  tone?: JimboBadgeTone
  children: React.ReactNode
}

/**
 * Small colored label pill. Matches Balatro's in-game tag/rarity badges.
 * All styling via jimbo.css `.j-badge` classes.
 */
export function JimboBadge({ size = 'sm', tone = 'dark', className, children, ...props }: JimboBadgeProps) {
  return (
    <span className={`j-badge j-badge--${size} j-badge--${tone} ${className ?? ''}`} {...props}>
      {children}
    </span>
  )
}
