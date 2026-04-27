"use client";

import React from "react";
import { JimboColorOption } from "../ui/tokens.js";
import { JimboText } from "../ui/jimboText.js";

export type JamlAestheticOption = "Palindrome" | "Psychosis" | "Gross" | "Nsfw" | "Funny" | "Balatro";

const AESTHETICS: { id: JamlAestheticOption; value: number; label: string; desc: string }[] = [
  { id: "Palindrome", value: 0, label: "Palindrome", desc: "Seeds that read the same forwards and backwards" },
  { id: "Psychosis", value: 1, label: "Psychosis", desc: "Unsettling or eerie seed patterns" },
  { id: "Gross", value: 2, label: "Gross", desc: "Seeds with crude or disgusting words" },
  { id: "Nsfw", value: 3, label: "NSFW", desc: "Seeds with adult content" },
  { id: "Funny", value: 4, label: "Funny", desc: "Seeds that spell funny words" },
  { id: "Balatro", value: 5, label: "Balatro", desc: "Seeds referencing the game itself" },
];

export interface JamlAestheticSelectorProps {
  value?: JamlAestheticOption | null;
  onChange: (aesthetic: JamlAestheticOption | null, numericValue: number) => void;
  className?: string;
  style?: React.CSSProperties;
}

export function JamlAestheticSelector({ value, onChange, className, style }: JamlAestheticSelectorProps) {
  return (
    <div
      className={className}
      style={{
        display: "flex",
        flexDirection: "column",
        gap: 4,
        ...style,
      }}
    >
      <JimboText size="xs" tone="grey">Seed Aesthetics</JimboText>
      <div style={{ display: "flex", flexWrap: "wrap", gap: 4 }}>
        {AESTHETICS.map((a) => {
          const isActive = value === a.id;
          return (
            <button
              key={a.id}
              type="button"
              onClick={() => onChange(isActive ? null : a.id, a.value)}
              title={a.desc}
              style={{
                padding: "4px 10px",
                borderRadius: 6,
                border: `2px solid ${isActive ? JimboColorOption.GOLD : JimboColorOption.PANEL_EDGE}`,
                background: isActive ? `${JimboColorOption.GOLD}22` : JimboColorOption.DARKEST,
                color: isActive ? JimboColorOption.GOLD_TEXT : JimboColorOption.GREY,
                cursor: "pointer",
                fontSize: 11,
                fontFamily: "m6x11plus, monospace",
                letterSpacing: 0.5,
                transition: "border-color 100ms, background 100ms",
              }}
            >
              {a.label}
            </button>
          );
        })}
      </div>
    </div>
  );
}
