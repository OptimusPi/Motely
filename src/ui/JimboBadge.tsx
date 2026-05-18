import React from 'react'

export type JimboBadgeTone = 'dark' | 'blue' | 'red' | 'green' | 'gold' | 'grey' | 'orange' | 'purple'

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
