'use client'

import React from 'react'
import { JimboLink } from './jimboLink'

export interface JimboBalatroFooterProps {
  /** Fade the footer out */
  hidden?: boolean;
  /** Extra className */
  className?: string;
  /** Optional inline children */
  children?: React.ReactNode;
}

/**
 * Fan-site attribution footer with Balatro link. The "Balatro" in the name is
 * load-bearing — this footer is the public disclosure that the project is a
 * non-profit, rule-following, PlayStack-aware fan site. Always rendered;
 * required attribution for using Balatro art.
 */
export function JimboBalatroFooter({ hidden = false, className = '', children }: JimboBalatroFooterProps) {
  if (hidden) {
    return null
  }

  return (
    <div className={["j-footer", className].filter(Boolean).join(" ")}>
      <div className="j-footer__bar">
        <p className="j-footer__line j-footer__line--wrap">
          <span className="j-footer__chunk">Not affiliated with LocalThunk or PlayStack</span>
          <span className="j-footer__chunk j-footer__chunk--credit">
            Made for the <JimboLink href="https://playbalatro.com">Balatro</JimboLink> community
          </span>
          {children ? <span className="j-footer__extra">{children}</span> : null}
        </p>
      </div>
    </div>
  )
}
