"use client";

import type { MotelyJamlyzerAnteResult } from "motely-wasm";
import { JimboPanel } from "../../ui/JimboPanel.js";
import { JimboInnerPanel } from "../../ui/panel.js";
import { JimboText } from "../../ui/jimboText.js";
import { JimboRow } from "../../ui/JimboLayout.js";
import { JamlVoucher } from "../GameCard.js";
import { decodeMotelyItem } from "../../decode/motelyItemDecoder.js";
import type { ParsedJamlClause } from "../../lib/jaml/parseClauses.js";
import { voucherDisplayName } from "./names.js";
import { itemTypeOfCategory, selectHighlight } from "./highlight.js";
import { JamlyzerItemCard } from "./JamlyzerItemCard.js";

export interface JamlyzerPullsProps {
  ante: MotelyJamlyzerAnteResult;
  matches: Map<string, ParsedJamlClause[]>;
}

/** Extra-card pulls (Judgement, Wraith, tags, …) and the voucher sequence. */
export function JamlyzerPulls({ ante, matches }: JamlyzerPullsProps) {
  const pulls = ante.pulls;
  const groups = [
    { title: "Judgement jokers", items: pulls.judgementJokers },
    { title: "Wraith jokers", items: pulls.wraithJokers },
    { title: "Emperor tarots", items: pulls.emperorTarots },
    { title: "Purple seal tarots", items: pulls.purpleSealTarots },
    { title: "Sixth Sense spectrals", items: pulls.sixthSenseSpectrals },
    { title: "Seance spectrals", items: pulls.seanceSpectrals },
    { title: "Riff-Raff jokers", items: pulls.riffRaffJokers },
    { title: "Rare tag jokers", items: pulls.rareTagJokers },
    { title: "Uncommon tag jokers", items: pulls.uncommonTagJokers },
    { title: "Legendary jokers", items: pulls.legendaryJokers },
  ].filter((g) => g.items.length > 0);

  if (groups.length === 0 && pulls.voucherSequence.length === 0) return null;

  return (
    <JimboPanel title="Pulls" tone="gold">
      {groups.map((group) => (
        <JimboInnerPanel key={group.title} className="j-stack j-stack--gap-sm">
          <JimboText size="xs" tone="grey">
            {group.title}
          </JimboText>
          <JimboRow wrap gap="sm" align="start">
            {group.items.map((item, i) => {
              const decoded = decodeMotelyItem(item);
              const highlight = decoded
                ? selectHighlight(
                    itemTypeOfCategory(decoded.category),
                    decoded.displayName,
                    ante.ante,
                    matches
                  )
                : undefined;
              return <JamlyzerItemCard key={i} item={item} highlight={highlight} />;
            })}
          </JimboRow>
        </JimboInnerPanel>
      ))}
      {pulls.voucherSequence.length > 0 && (
        <JimboInnerPanel className="j-stack j-stack--gap-sm">
          <JimboText size="xs" tone="grey">
            Voucher sequence
          </JimboText>
          <JimboRow wrap gap="sm" align="start">
            {pulls.voucherSequence.map((voucher, i) => {
              const name = voucherDisplayName(voucher);
              const highlight = selectHighlight("voucher", name, ante.ante, matches);
              return (
                <JamlVoucher key={i} voucherName={name} scale={0.75} className={highlight ?? ""} />
              );
            })}
          </JimboRow>
        </JimboInnerPanel>
      )}
    </JimboPanel>
  );
}
