"use client";

<<<<<<< HEAD
import { JamlAesthetic } from "motely-wasm";
import { JimboBadge } from "../ui/JimboBadge.js";
import { JimboPanelSpinner } from "../ui/JimboPanelSpinner.js";

const AESTHETICS: { id: JamlAesthetic; label: string; desc: string }[] = [
  { id: JamlAesthetic.Palindrome, label: "Palindrome", desc: "Seeds that read the same forwards and backwards" },
  { id: JamlAesthetic.Psychosis, label: "Psychosis", desc: "Unsettling or eerie seed patterns" },
=======
import { JamlAesthetic } from "motely-wasm/motely/filters/jaml";
import { JimboBadge } from "../ui/JimboBadge.js";
import { JimboPanelSpinner } from "../ui/JimboPanelSpinner.js";

// "Echo" is the engine's forthcoming native name for this aesthetic. Until the
// upstream MotelyJAML rename ships, the installed engine exposes it under its
// index-1 identifier, so resolve to whichever member the engine actually
// defines — always an engine value, never a hardcoded index. This adopts native
// `Echo` automatically once it lands, with no further change needed here.
const ENGINE_AESTHETIC = JamlAesthetic as unknown as Record<string, JamlAesthetic | undefined>;
const ECHO_AESTHETIC: JamlAesthetic = ENGINE_AESTHETIC.Echo ?? JamlAesthetic.Echo;

const AESTHETICS: { id: JamlAesthetic; label: string; desc: string }[] = [
  { id: JamlAesthetic.Palindrome, label: "Palindrome", desc: "Seeds that read the same forwards and backwards" },
  { id: ECHO_AESTHETIC, label: "Echo", desc: "Seeds with an echoing pattern (ABAxBxxx)" },
>>>>>>> 4c1c0b639ac307d7366dccd1170ebadffbc2ab45
  { id: JamlAesthetic.Gross, label: "Gross", desc: "Seeds with crude or disgusting words" },
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
