"use client";

import type { MotelyJamlyzerAnteResult } from "motely-wasm";
import { JimboInnerPanel } from "../../ui/panel.js";
import { JimboText } from "../../ui/jimboText.js";
import { JimboBadge } from "../../ui/JimboBadge.js";
import { JimboRow } from "../../ui/JimboLayout.js";
import { decodeMotelyItem } from "../../decode/motelyItemDecoder.js";
import type { ParsedJamlClause } from "../../lib/jaml/parseClauses.js";
import { packDisplayName } from "./names.js";
import { itemTypeOfCategory, selectHighlight } from "./highlight.js";
import { JamlyzerItemCard } from "./JamlyzerItemCard.js";

export interface JamlyzerPackSectionProps {
  pack: MotelyJamlyzerAnteResult["packs"][number];
  ante: number;
  matches: Map<string, ParsedJamlClause[]>;
}

/** One booster pack: name + card count, then the cards inside. */
export function JamlyzerPackSection({ pack, ante, matches }: JamlyzerPackSectionProps) {
  return (
    <JimboInnerPanel className="j-stack j-stack--gap-md">
      <JimboRow gap="md">
        <JimboText size="md">{packDisplayName(pack.pack)}</JimboText>
        <JimboBadge tone="blue" size="sm">
          {pack.items.length} cards
        </JimboBadge>
      </JimboRow>
      <JimboRow wrap gap="sm" align="start">
        {pack.items.map((item, i) => {
          const decoded = decodeMotelyItem(item);
          const highlight = decoded
            ? selectHighlight(itemTypeOfCategory(decoded.category), decoded.displayName, ante, matches)
            : undefined;
          return <JamlyzerItemCard key={i} item={item} highlight={highlight} />;
        })}
      </JimboRow>
    </JimboInnerPanel>
  );
}
