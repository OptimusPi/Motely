import React from 'react'
import { JimboColorOption as C } from './tokens.js'

export interface JimboBadgeProps {
  size?: 'sm' | 'md'
  tone?: 'dark' | 'blue' | 'red' | 'green' | 'gold' | 'grey'
  children: React.ReactNode
}

export function JimboBadge({ size = 'sm', tone = 'dark', children }: JimboBadgeProps) {
  const bgColors = {
    dark: C.DARKEST,
    blue: C.BLUE,
    red: C.RED,
    green: C.GREEN,
    gold: C.GOLD,
    grey: C.GREY
  };

  return (
    <span style={{
      display: 'inline-flex',
      alignItems: 'center',
      padding: size === 'sm' ? '2px 6px' : '4px 8px',
      fontSize: size === 'sm' ? 10 : 12,
      background: bgColors[tone],
      color: tone === 'dark' || tone === 'grey' ? C.WHITE : C.DARKEST,
      border: `1px solid ${tone === 'dark' ? C.PANEL_EDGE : C.BLACK}`,
      borderRadius: 4,
      fontFamily: 'var(--font-sans, m6x11plus), monospace',
      whiteSpace: 'nowrap'
    }}>
      {children}
    </span>
  )
}
