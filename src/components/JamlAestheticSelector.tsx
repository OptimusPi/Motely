"use client";

import { JimboBadge } from "../ui/JimboBadge.js";
import { JimboPanelSpinner } from "../ui/JimboPanelSpinner.js";

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
 * Spinner-style aesthetic selector for seed filters.
 * Uses left/right controls plus a centered badge value display.
 */
export function JamlAestheticSelector({ value, onChange, className, style }: JamlAestheticSelectorProps) {
  const currentIndex = value == null ? -1 : AESTHETICS.findIndex((a) => a.id === value);
  const current = currentIndex >= 0 ? AESTHETICS[currentIndex] : null;

  const step = (direction: -1 | 1) => {
    const length = AESTHETICS.length;

    // Include null as "Any" in the spinner cycle: Any -> option0 -> ... -> Any
    const cycleIndex = currentIndex + 1;
    const nextCycleIndex = (cycleIndex + direction + (length + 1)) % (length + 1);

    if (nextCycleIndex === 0) {
      onChange(null, -1);
      return;
    }

    const next = AESTHETICS[nextCycleIndex - 1];
    onChange(next.id, next.value);
  };

  const label = current?.label ?? "Any";
  const numericValue = current?.value ?? -1;
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
