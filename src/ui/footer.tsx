'use client'

import React from 'react'

const SUITS = [
  { char: '♥️', kf: 'jaml-heart' },
  { char: '♠️', kf: 'jaml-spade' },
  { char: '♦️', kf: 'jaml-diamond' },
  { char: '♣️', kf: 'jaml-club' },
] as const

export interface JimboBalatroFooterProps {
  hidden?: boolean
  className?: string
}

/**
 * Attribution footer with animated suit cycle.
 * All styling via jimbo.css `.j-footer` classes — zero inline styles.
 */
export function JimboBalatroFooter({ hidden = false, className = '' }: JimboBalatroFooterProps) {
  return (
    <div className={`j-footer ${hidden ? 'j-footer--hidden' : ''} ${className}`}>
      <div className="j-footer__bar">
        <p className="j-footer__text">
          <span>Not affiliated with LocalThunk or PlayStack •{' '}</span>
          <a
              href="https://store.steampowered.com/app/2379780/Balatro/"
              target="_blank"
              rel="noopener noreferrer"
              className="j-footer__link"
          >
              BUY BALATRO
          </a>
          <span>{' '}• Created with{' '}</span>
          <span className="j-footer__suits">
            <span className="j-footer__suit-stage">
              {SUITS.map(({ char, kf }) => (
                <span key={char} className="j-footer__suit-char" style={{ animationName: kf }}>
                  {char}
                </span>
              ))}
            </span>{' '}
            for the Balatro community
          </span>
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
