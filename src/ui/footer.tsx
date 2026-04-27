'use client'

import React from 'react'
import { JimboColorOption } from './tokens.js'

const SUITS = [
  { char: '♥️', kf: 'jaml-heart' },
  { char: '♠️', kf: 'jaml-spade' },
  { char: '♦️', kf: 'jaml-diamond' },
  { char: '♣️', kf: 'jaml-club' },
] as const

const CYCLE = '5s'

export interface JimboBalatroFooterProps {
  hidden?: boolean
  className?: string
}

export function JimboBalatroFooter({ hidden = false, className = '' }: JimboBalatroFooterProps) {
  return (
    <div
      className={['w-full transition-opacity duration-200', hidden ? 'pointer-events-none opacity-0' : 'opacity-100', className].filter(Boolean).join(' ')}
    >
      <div style={{ width: '100%', borderTop: '1px solid rgba(255,255,255,0.1)', background: 'rgba(0,0,0,0.9)', padding: '0 1rem 3px', textAlign: 'center' }}>
        <p style={{ fontFamily: 'm6x11plus, monospace', fontSize: 'clamp(11px, 0.8vw + 8px, 14px)', display: 'flex', flexWrap: 'wrap', alignItems: 'center', justifyContent: 'center', gap: '0 0.5rem', color: 'white', margin: 0 }}>
          <span>Not affiliated with LocalThunk or PlayStack</span>
          <span style={{ display: 'inline-flex', alignItems: 'center', gap: '0.25rem' }}>
            Made with{' '}
            <span style={{ position: 'relative', display: 'inline-block', width: '1.5em', height: '1em', verticalAlign: 'middle' }}>
              {SUITS.map(({ char, kf }) => (
                <span key={char} style={{ position: 'absolute', inset: 0, display: 'inline-flex', alignItems: 'center', justifyContent: 'center', opacity: 0, animationName: kf, animationDuration: CYCLE, animationDelay: '0s', animationIterationCount: 'infinite', animationTimingFunction: 'ease-out' }}>
                  {char}
                </span>
              ))}
            </span>{' '}
            for the{' '}
            <a href="https://playbalatro.com" target="_blank" rel="noopener noreferrer" style={{ color: JimboColorOption.GOLD, textDecoration: 'none' }}>Balatro</a>{' '}
            community
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
