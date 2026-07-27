"use client";

import { useMemo } from "react";
import type { MotelyItem } from "motely-wasm";
import { JamlGameCard } from "../GameCard.js";
import { JimboBadge } from "../../ui/JimboBadge.js";
import { decodeMotelyItemToJamlCard } from "../../decode/motelyItemDecoder.js";

export interface JamlyzerItemCardProps {
  item: MotelyItem;
  scale?: number;
  /** Glow class from selectHighlight, applied to the card itself. */
  highlight?: string;
}

/** One decoded Motely item rendered as a game card, with clause-match glow. */
export function JamlyzerItemCard({ item, scale = 0.85, highlight }: JamlyzerItemCardProps) {
  const resolved = useMemo(() => decodeMotelyItemToJamlCard(item, scale), [item, scale]);
  if (!resolved) {
    return (
      <JimboBadge size="md" tone="grey" title="Unrecognized item">
        ?
      </JimboBadge>
    );
  }
  return (
    <JamlGameCard type={resolved.type} card={resolved.card} hoverTilt className={highlight ?? ""} />
  );
}
