"use client";

import type { MotelyItem } from "motely-wasm";
import { JimboPanel } from "../../ui/JimboPanel.js";
import { JimboRow } from "../../ui/JimboLayout.js";
import { StandardCard } from "../StandardCard.js";
import { decodeMotelyItemToJamlCard } from "../../decode/motelyItemDecoder.js";
import type { ParsedJamlClause } from "../../lib/jaml/parseClauses.js";
import { selectHighlight } from "./highlight.js";
import { JamlyzerItemCard } from "./JamlyzerItemCard.js";

export interface JamlyzerErraticDeckProps {
  cards: MotelyItem[];
  matches: Map<string, ParsedJamlClause[]>;
}

/** The randomized starting deck when playing the Erratic deck. */
export function JamlyzerErraticDeck({ cards, matches }: JamlyzerErraticDeckProps) {
  if (cards.length === 0) return null;
  return (
    <JimboPanel title="Erratic deck" tone="gold">
      <JimboRow wrap gap="sm" align="start">
        {cards.map((item, i) => {
          const resolved = decodeMotelyItemToJamlCard(item, 0.65);
          if (resolved?.type === "playing") {
            const name = `${resolved.card.rank} of ${resolved.card.suit}`;
            const highlight = selectHighlight("standardcard", name, 0, matches);
            return (
              <StandardCard
                key={i}
                rank={resolved.card.rank as never}
                suit={resolved.card.suit as never}
                enhancement={resolved.card.enhancements?.[0] as never}
                seal={resolved.card.seal as never}
                edition={resolved.card.edition as never}
                size={48}
                className={highlight ?? ""}
              />
            );
          }
          return <JamlyzerItemCard key={i} item={item} scale={0.65} />;
        })}
      </JimboRow>
    </JimboPanel>
  );
}
