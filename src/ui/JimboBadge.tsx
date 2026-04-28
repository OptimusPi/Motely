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

  // Red and Blue get white text per DESIGN.md. Gold/Green/Grey get black text. Darkest gets white.
  const textColors = {
    dark: C.WHITE,
    blue: C.WHITE,
    red: C.WHITE,
    green: C.BLACK,
    gold: C.BLACK,
    grey: C.WHITE
  };

  return (
    <span style={{
      display: 'inline-flex',
      alignItems: 'center',
      padding: size === 'sm' ? '2px 6px' : '4px 8px',
      fontSize: size === 'sm' ? 10 : 12,
      background: bgColors[tone],
      color: textColors[tone],
      border: `1px solid ${tone === 'dark' ? C.PANEL_EDGE : C.BLACK}`,
      borderRadius: 4,
      fontFamily: "'m6x11plus', 'Courier New', monospace",
      textTransform: 'uppercase',
      letterSpacing: '0.04em',
      whiteSpace: 'nowrap'
    }}>
      {children}
    </span>
  )
}
