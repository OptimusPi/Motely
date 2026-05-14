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
    fontSize: "clamp(11px, 0.8vw + 8px, 14px)",
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
  return (
    <div
      className={["j-footer transition-opacity duration-200", hidden ? "pointer-events-none opacity-0" : "opacity-100", className].filter(Boolean).join(" ")}
      style={{ width: "100%", borderTop: "1px solid rgba(255,255,255,0.1)", background: "rgba(0,0,0,0.9)", padding: "0 1rem 3px", textAlign: "center" }}
    >
      <p style={{ ...BASE_STYLE, display: "flex", flexWrap: "wrap", alignItems: "center", justifyContent: "center", gap: "0 0.5rem", color: "white", margin: 0, position: "relative" }}>
        <span>Not affiliated with LocalThunk or PlayStack</span>
        <span style={{ display: "inline-flex", alignItems: "center", gap: "0.25rem" }}>
          Made with{" "}
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
          </span>{" "}
          for the{" "}
          <a href="https://playbalatro.com" target="_blank" rel="noopener noreferrer" style={{ color: BALATRO_GOLD, textDecoration: "none" }}>Balatro</a>{" "}
          community
        </span>
        {children && <span style={{ display: "inline-flex", alignItems: "center", marginLeft: "0.5rem" }}>{children}</span>}
      </p>

      <style>{`
        @keyframes bff-heart {
            0%    { opacity: 0; transform: scale(1);    }
            1%    { opacity: 1; transform: scale(1.45); }
            3.5%  { opacity: 1; transform: scale(1);    }
            61.5% { opacity: 1; transform: scale(1);    }
            62%   { opacity: 0; transform: scale(1);    }
            100%  { opacity: 0; transform: scale(1);    }
        }
        @keyframes bff-spade {
            0%,  61.5% { opacity: 0; transform: scale(1);    }
            62%        { opacity: 1; transform: scale(1.45);  }
            64.5%      { opacity: 1; transform: scale(1);     }
            71.5%      { opacity: 1; transform: scale(1);     }
            72%        { opacity: 0; transform: scale(1);     }
            100%       { opacity: 0; }
        }
        @keyframes bff-diamond {
            0%,  71.5% { opacity: 0; transform: scale(1);    }
            72%        { opacity: 1; transform: scale(1.45);  }
            74.5%      { opacity: 1; transform: scale(1);     }
            81.5%      { opacity: 1; transform: scale(1);     }
            82%        { opacity: 0; transform: scale(1);     }
            100%       { opacity: 0; }
        }
        @keyframes bff-club {
            0%,  81.5% { opacity: 0; transform: scale(1);    }
            82%        { opacity: 1; transform: scale(1.45);  }
            84.5%      { opacity: 1; transform: scale(1);     }
            95%        { opacity: 1; transform: scale(1);     }
            96%        { opacity: 0; transform: scale(1);     }
            100%       { opacity: 0; }
        }
      `}</style>
    </div>
  )
}
