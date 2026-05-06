"use client";

import React from "react";
import { JimboText } from "../ui/jimboText.js";
import { JimboButton } from "../ui/panel.js";

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

/**
 * Pill-toggle selector for seed aesthetic filters.
 * All styling via jimbo.css `.j-aesthetic-selector` / `.j-aesthetic-pill` — zero inline styles.
 */
export function JamlAestheticSelector({ value, onChange, className, style }: JamlAestheticSelectorProps) {
  return (
    <div className={`j-aesthetic-selector ${className ?? ""}`} style={style}>
      <JimboText size="xs" tone="grey">Seed Aesthetics</JimboText>
      <div style={{ display: "flex", flexWrap: "wrap", gap: 6, marginTop: 4 }}>
        {AESTHETICS.map((a) => {
          const isActive = value === a.id;
          return (
            <JimboButton
              key={a.id}
              tone={isActive ? "red" : "grey"}
              size="sm"
              onClick={() => onChange(isActive ? null : a.id, a.value)}
            >
              {a.label}
            </JimboButton>
          );
        })}
      </div>
    </div>
  );
}
