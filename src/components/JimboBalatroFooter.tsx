'use client'

import React from 'react'

const SUITS = [
  { char: '♥', kf: 'jaml-heart' },
  { char: '♠', kf: 'jaml-spade' },
  { char: '♦', kf: 'jaml-diamond' },
  { char: '♣', kf: 'jaml-club' },
] as const

export interface JimboBalatroFooterProps {
  /** Fade the footer out */
  hidden?: boolean;
  /** Extra className */
  className?: string;
  /** Inline style override — e.g. `{ position: 'static' }` to opt out of the
   * default fixed-to-viewport placement when embedding outside the 320×568
   * MCP App frame. */
  style?: React.CSSProperties;
  /** Optional inline children */
  children?: React.ReactNode;
}

/**
 * Fan-site attribution footer with Balatro link. The "Balatro" in the name is
 * load-bearing — this footer is the public disclosure that the project is a
 * non-profit, rule-following, PlayStack-aware fan site. Always rendered;
 * required attribution for using Balatro art.
 */
export function JimboBalatroFooter({ hidden = false, className = '', style, children }: JimboBalatroFooterProps) {
  if (hidden) {
    return null
  }

  return (
    <div className={["j-footer", className].filter(Boolean).join(" ")} style={style}>
      <div className="j-footer__bar">
        <p className="j-footer__line j-footer__line--wrap">
          <span className="j-footer__chunk">Not affiliated with LocalThunk or Playstack</span>
          <span className="j-footer__chunk j-footer__chunk--credit">
            Made with{' '}
            <span className="j-footer__suits">
              <span className="j-footer__suit-stage">
                {SUITS.map(({ char, kf }) => (
                  <span key={char} className="j-footer__suit-char" style={{ animationName: kf }}>
                    {char}
                  </span>
                ))}
              </span>
            </span>{' '}
            for the <a className="j-link" href="https://playbalatro.com" target="_blank" rel="noreferrer">Balatro</a> community
          </span>
          {children ? <span className="j-footer__extra">{children}</span> : null}
        </p>
      </div>
      <style>{`
        @keyframes jaml-heart   { 0%{opacity:0;transform:scale(1)} 1%{opacity:1;transform:scale(1.45)} 3.5%{opacity:1;transform:scale(1)} 61.5%{opacity:1;transform:scale(1)} 62%{opacity:0} 100%{opacity:0} }
        @keyframes jaml-spade   { 0%,61.5%{opacity:0} 62%{opacity:1;transform:scale(1.45)} 64.5%{opacity:1;transform:scale(1)} 71.5%{opacity:1} 72%{opacity:0} 100%{opacity:0} }
        @keyframes jaml-diamond { 0%,71.5%{opacity:0} 72%{opacity:1;transform:scale(1.45)} 74.5%{opacity:1;transform:scale(1)} 81.5%{opacity:1} 82%{opacity:0} 100%{opacity:0} }
        @keyframes jaml-club    { 0%,81.5%{opacity:0} 82%{opacity:1;transform:scale(1.45)} 84.5%{opacity:1;transform:scale(1)} 95%{opacity:1}  96%{opacity:0} 100%{opacity:0} }
      `}</style>
    </div>
  )
}
