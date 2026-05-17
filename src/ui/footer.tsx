'use client'

import React from 'react'

const SUITS = [
    { char: "♥️", keyframe: "bff-heart" },
    { char: "♠️", keyframe: "bff-spade" },
    { char: "♦️", keyframe: "bff-diamond" },
    { char: "♣️", keyframe: "bff-club" },
] as const;

const CYCLE = "5s";
const BASE_STYLE: React.CSSProperties = {
    fontFamily: "var(--font-game, var(--j-font, ui-sans-serif, system-ui, sans-serif))",
  fontSize: "10px",
};
const BALATRO_GOLD = "#FFD85C";

export interface JimboBalatroFooterProps {
  /** Fade the footer out */
  hidden?: boolean;
  /** Extra className */
  className?: string;
  /** Optional inline children */
  children?: React.ReactNode;
}

/**
 * Attribution footer with animated suit cycle.
 * Always rendered — required attribution for using Balatro art.
 */
export function JimboBalatroFooter({ hidden = false, className = '', children }: JimboBalatroFooterProps) {
  if (hidden) {
    return null
  }

  return (
    <div className={["j-footer", className].filter(Boolean).join(" ")}>
      <div className="j-footer__bar">
        <p className="j-footer__line j-footer__line--wrap" style={BASE_STYLE}>
          <span>Not affiliated with LocalThunk or PlayStack</span>
          <span className="j-footer__sep">•</span>
          <span>Made with</span>
          <span style={{ position: "relative", display: "inline-block", width: "1.5em", height: "1em", verticalAlign: "middle" }}>
            {SUITS.map(({ char, keyframe }) => (
              <span
                key={char}
                style={{
                  position: "absolute",
                  inset: 0,
                  display: "inline-flex",
                  alignItems: "center",
                  justifyContent: "center",
                  opacity: 0,
                  animationName: keyframe,
                  animationDuration: CYCLE,
                  animationDelay: "0s",
                  animationIterationCount: "infinite",
                  animationTimingFunction: "ease-out",
                }}
              >
                {char}
              </span>
            ))}
          </span>
          <span>for the</span>
          <a className="j-footer__link" href="https://playbalatro.com" target="_blank" rel="noopener noreferrer" style={{ color: BALATRO_GOLD }}>
            Balatro
          </a>
          <span>community</span>
          {children ? <span className="j-footer__extra">{children}</span> : null}
        </p>
      </div>

      <style>{`
        @keyframes bff-heart {
          0%    { opacity: 0; transform: translateY(0); }
          1%    { opacity: 1; transform: translateY(-2px); }
          3.5%  { opacity: 1; transform: translateY(0); }
          61.5% { opacity: 1; transform: translateY(0); }
          62%   { opacity: 0; transform: translateY(0); }
          100%  { opacity: 0; transform: translateY(0); }
        }
        @keyframes bff-spade {
          0%,  61.5% { opacity: 0; transform: translateY(0); }
          62%        { opacity: 1; transform: translateY(-2px); }
          64.5%      { opacity: 1; transform: translateY(0); }
          71.5%      { opacity: 1; transform: translateY(0); }
          72%        { opacity: 0; transform: translateY(0); }
            100%       { opacity: 0; }
        }
        @keyframes bff-diamond {
          0%,  71.5% { opacity: 0; transform: translateY(0); }
          72%        { opacity: 1; transform: translateY(-2px); }
          74.5%      { opacity: 1; transform: translateY(0); }
          81.5%      { opacity: 1; transform: translateY(0); }
          82%        { opacity: 0; transform: translateY(0); }
            100%       { opacity: 0; }
        }
        @keyframes bff-club {
          0%,  81.5% { opacity: 0; transform: translateY(0); }
          82%        { opacity: 1; transform: translateY(-2px); }
          84.5%      { opacity: 1; transform: translateY(0); }
          95%        { opacity: 1; transform: translateY(0); }
          96%        { opacity: 0; transform: translateY(0); }
            100%       { opacity: 0; }
        }
      `}</style>
    </div>
  )
}
