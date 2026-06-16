'use client'

import React from 'react'
<<<<<<< HEAD

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
=======
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
>>>>>>> 4c1c0b639ac307d7366dccd1170ebadffbc2ab45
  )
}

/** Scrollable content area inside JimboApp. Hidden scrollbar, snap-friendly. */
<<<<<<< HEAD
export function JimboAppScroll({ children, className = '', ...props }: Omit<JimboAppProps, 'fluid'>) {
=======
export function JimboAppScroll({ children, className = '', ...props }: JimboAppProps) {
>>>>>>> 4c1c0b639ac307d7366dccd1170ebadffbc2ab45
  return (
    <div className={`j-app__scroll ${className}`} {...props}>
      {children}
    </div>
  )
}

/** Sticky bottom action area inside JimboApp. Thumb zone. */
<<<<<<< HEAD
export function JimboAppFooter({ children, className = '', ...props }: Omit<JimboAppProps, 'fluid'>) {
=======
export function JimboAppFooter({ children, className = '', ...props }: JimboAppProps) {
>>>>>>> 4c1c0b639ac307d7366dccd1170ebadffbc2ab45
  return (
    <div className={`j-app__footer ${className}`} {...props}>
      {children}
    </div>
  )
}
