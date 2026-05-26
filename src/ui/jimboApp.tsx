'use client'

import React from 'react'

// ─── App Shell ──────────────────────────────────────────────────────────────
// iPhone SE 5 portrait — 320×568 LOCKED. Content inside is designed to fit
// perfectly; the panel itself does not resize, drag, or reflow.
//
// Add `fluid` prop to unlock for MCP / desktop contexts (j-app--fluid).
// In fluid mode the container stretches to fill its parent (up to 750px)
// and container queries in jimbo.css activate "cozy" overrides at 401px+.

export interface JimboAppProps extends React.HTMLAttributes<HTMLDivElement> {
  children: React.ReactNode
  /** Unlock width/height for MCP inline or desktop use. Default: false (320×568 locked iPhone SE 5). */
  fluid?: boolean
}

/** iPhone SE 5 app shell. 320×568 locked, or fluid for MCP/desktop. */
export function JimboApp({ children, fluid, className = '', ...props }: JimboAppProps) {
  const classes = `j-app${fluid ? ' j-app--fluid' : ''} ${className}`.trim()
  return (
    <div className={classes} {...props}>
      {children}
    </div>
  )
}

/** Scrollable content area inside JimboApp. Hidden scrollbar, snap-friendly. */
export function JimboAppScroll({ children, className = '', ...props }: Omit<JimboAppProps, 'fluid'>) {
  return (
    <div className={`j-app__scroll ${className}`} {...props}>
      {children}
    </div>
  )
}

/** Sticky bottom action area inside JimboApp. Thumb zone. */
export function JimboAppFooter({ children, className = '', ...props }: Omit<JimboAppProps, 'fluid'>) {
  return (
    <div className={`j-app__footer ${className}`} {...props}>
      {children}
    </div>
  )
}
