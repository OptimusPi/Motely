"use client";

import { JamlAesthetic } from "motely-wasm/motely/filters/jaml";
import { JimboBadge } from "../ui/JimboBadge.js";
import { JimboPanelSpinner } from "../ui/JimboPanelSpinner.js";

const AESTHETICS: { id: JamlAesthetic; label: string; desc: string }[] = [
  { id: JamlAesthetic.Palindrome, label: "Palindrome", desc: "Seeds that read the same forwards and backwards" },
  { id: JamlAesthetic.Psychosis, label: "Psychosis", desc: "Unsettling or eerie seed patterns" },
  { id: JamlAesthetic.Gross, label: "Gross", desc: "Seeds with crude or disgusting words" },
  { id: JamlAesthetic.Nsfw, label: "Nsfw", desc: "Seeds with adult content" },
  { id: JamlAesthetic.Funny, label: "Funny", desc: "Seeds that spell funny words" },
  { id: JamlAesthetic.Balatro, label: "Balatro", desc: "Seeds referencing the game itself" },
];

export interface JamlAestheticSelectorProps {
  value?: JamlAesthetic | null;
  onChange: (aesthetic: JamlAesthetic | null) => void;
  className?: string;
  style?: React.CSSProperties;
}

/**
 * Spinner-style aesthetic selector for seed filters.
 * Uses left/right controls plus a centered badge value display.
 */
export function JamlAestheticSelector({ value, onChange, className, style }: JamlAestheticSelectorProps) {
  const currentIndex = value == null ? -1 : AESTHETICS.findIndex((a) => a.id === value);
  const current = currentIndex >= 0 ? AESTHETICS[currentIndex] : null;

  const step = (direction: -1 | 1) => {
    const length = AESTHETICS.length;
    const cycleIndex = currentIndex + 1;
    const nextCycleIndex = (cycleIndex + direction + (length + 1)) % (length + 1);

    if (nextCycleIndex === 0) {
      onChange(null);
      return;
    }

    onChange(AESTHETICS[nextCycleIndex - 1].id);
  };

  const label = current?.label ?? "Any";
  const numericValue = current?.id ?? -1;
  const description = current?.desc ?? "No aesthetic constraint";

  return (
    <JimboPanelSpinner
      label="Seed aesthetics"
      title={label}
      description={description}
      meta={<JimboBadge size="md" tone={current ? "purple" : "dark"}>{numericValue}</JimboBadge>}
      onPrev={() => step(-1)}
      onNext={() => step(1)}
      className={className}
      style={style}
    />
  );
}
