'use client'

import React from 'react'
import { JimboBalatroFooter } from './JimboBalatroFooter'

// ─── App Shell ──────────────────────────────────────────────────────────────
// 320×540 hard-locked (iPhone SE portrait 568, minus the 28px JimboBalatroFooter
// that sits below it). The MCP Apps inline embed target. One size — no fluid,
// no responsive, no reflow, ever.
//
// JimboApp owns the JimboBalatroFooter render — Balatro attribution is
// legally required wherever the app appears, so it's bound to the app shell,
// not to the (aesthetic, optional) swirl background.

export interface JimboAppProps extends React.HTMLAttributes<HTMLDivElement> {
  children: React.ReactNode
}

export function JimboApp({ children, className = '', ...props }: JimboAppProps) {
  return (
    <>
      <div className={`j-app ${className}`.trim()} {...props}>
        {children}
      </div>
      <JimboBalatroFooter />
    </>
  )
}

/** Scrollable content area inside JimboApp. Hidden scrollbar, snap-friendly. */
export function JimboAppScroll({ children, className = '', ...props }: JimboAppProps) {
  return (
    <div className={`j-app__scroll ${className}`} {...props}>
      {children}
    </div>
  )
}

/** Sticky bottom action area inside JimboApp. Thumb zone. */
export function JimboAppFooter({ children, className = '', ...props }: JimboAppProps) {
  return (
    <div className={`j-app__footer ${className}`} {...props}>
      {children}
    </div>
  )
}
