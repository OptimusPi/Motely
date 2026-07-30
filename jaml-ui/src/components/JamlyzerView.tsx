"use client";

import React, { useMemo, useState } from "react";
import type { MotelyJamlyzerSeedResult } from "motely-wasm";
import { MotelyDeck, MotelyStake } from "motely-wasm";
import { JimboPanel } from "../ui/JimboPanel.js";
import { JimboInnerPanel } from "../ui/panel.js";
import { JimboText } from "../ui/jimboText.js";
import { JimboStack, JimboRow } from "../ui/JimboLayout.js";
import { JimboSeedCopyChip } from "../ui/JimboSeedCopyChip.js";
import { JamlVoucher, JamlTag, JamlBoss } from "./GameCard.js";
import { decodeMotelyItem } from "../decode/motelyItemDecoder.js";
import {
  parseJamlClauses,
  type ParsedJamlClause,
} from "../lib/jaml/parseClauses.js";
import {
  bossDisplayName,
  tagDisplayName,
  voucherDisplayName,
  deckDisplayName,
  stakeDisplayName,
} from "./jamlyzer/names.js";
import { buildMatchMap, selectHighlight, itemTypeOfCategory } from "./jamlyzer/highlight.js";
import { JamlyzerItemCard } from "./jamlyzer/JamlyzerItemCard.js";
import { JamlyzerPackSection } from "./jamlyzer/JamlyzerPackSection.js";
import { JamlyzerPulls } from "./jamlyzer/JamlyzerPulls.js";
import { JamlyzerEvents } from "./jamlyzer/JamlyzerEvents.js";
import { JamlyzerErraticDeck } from "./jamlyzer/JamlyzerErraticDeck.js";
import { JamlyzerRail } from "./jamlyzer/JamlyzerRail.js";

export interface JamlyzerViewProps {
  result: MotelyJamlyzerSeedResult;
  deck?: MotelyDeck;
  stake?: MotelyStake;
  /** Maximum ante number to display in the rail. Antes 0–39 are valid in Balatro. */
  maxAnte?: number;
  /** Raw JAML text used to derive clause identities for highlighting. */
  jamlText?: string;
  /** Pre-parsed clauses (alternative to `jamlText`). */
  clauses?: ParsedJamlClause[];
  /** Per-should-clause tally values, in JAML order. */
  tallies?: number[] | Int32Array;
  /** Called when the user hovers a clause in the scoreboard. */
  onHoverClause?: (clause: ParsedJamlClause | null) => void;
}

function blindCellClass(highlight: string | undefined): string {
  return ["j-row", "j-row--gap-md", "j-row--align-center", highlight].filter(Boolean).join(" ");
}

export function JamlyzerView({
  result,
  deck,
  stake,
  maxAnte: maxAnteProp,
  jamlText,
  clauses: clausesProp,
  tallies,
  onHoverClause,
}: JamlyzerViewProps) {
  const maxAnte = maxAnteProp ?? 39;
  const [selectedAnte, setSelectedAnte] = useState<number>(() => result.antes[0]?.ante ?? 0);
  const [hoveredClause, setHoveredClause] = useState<ParsedJamlClause | null>(null);

  const parsed = useMemo(() => {
    if (clausesProp) return clausesProp;
    if (jamlText) return parseJamlClauses(jamlText).all;
    return [];
  }, [clausesProp, jamlText]);

  const matches = useMemo(() => buildMatchMap(parsed), [parsed]);
  const activeMatches = useMemo(() => {
    if (!hoveredClause) return matches;
    const m = new Map<string, ParsedJamlClause[]>();
    matches.forEach((clauses, key) => {
      const filtered = clauses.filter((c) => c === hoveredClause);
      if (filtered.length > 0) m.set(key, filtered);
    });
    return m;
  }, [matches, hoveredClause]);

  const availableAntes = useMemo(
    () => new Set(result.antes.map((a) => a.ante)),
    [result.antes]
  );

  const anteNumbers = useMemo(
    () => Array.from({ length: maxAnte + 1 }, (_, i) => i),
    [maxAnte]
  );

  const ante = useMemo(
    () => result.antes.find((a) => a.ante === selectedAnte) ?? result.antes[0],
    [result.antes, selectedAnte]
  );

  const handleClauseHover = (clause: ParsedJamlClause | null) => {
    setHoveredClause(clause);
    onHoverClause?.(clause);
  };

  if (!ante) {
    return (
      <JimboPanel body>
        <JimboText tone="grey">No ante data available for seed {result.seed}.</JimboText>
      </JimboPanel>
    );
  }

  const smallTag = tagDisplayName(ante.smallBlindTag);
  const bigTag = tagDisplayName(ante.bigBlindTag);
  const boss = bossDisplayName(ante.boss);
  const voucher = voucherDisplayName(ante.voucher);

  return (
    <JimboPanel body={false} className="j-jamlyzer-view">
      <JamlyzerRail
        clauses={parsed}
        tallies={tallies}
        anteNumbers={anteNumbers}
        availableAntes={availableAntes}
        selectedAnte={selectedAnte}
        onSelectAnte={setSelectedAnte}
        onHoverClause={handleClauseHover}
      />

      <JimboStack gap="lg" align="stretch">
        <JimboPanel title="Seed" tone="gold">
          <JimboRow wrap gap="lg" align="center">
            <JimboSeedCopyChip value={result.seed} />
            <JimboText tone="grey">
              Score: <JimboText tone="gold">{result.score}</JimboText>
            </JimboText>
            {deck !== undefined && (
              <JimboText tone="grey">
                Deck: <JimboText>{deckDisplayName(deck)}</JimboText>
              </JimboText>
            )}
            {stake !== undefined && (
              <JimboText tone="grey">
                Stake: <JimboText>{stakeDisplayName(stake)}</JimboText>
              </JimboText>
            )}
          </JimboRow>
        </JimboPanel>

        <JimboPanel title="Blinds" tone="gold">
          <JimboRow wrap gap="md" align="center">
            <JimboInnerPanel
              className={blindCellClass(selectHighlight("tag", smallTag, ante.ante, activeMatches))}
            >
              <JimboText size="xs" tone="grey">
                Small
              </JimboText>
              <JamlTag tagName={smallTag} scale={0.75} />
            </JimboInnerPanel>
            <JimboInnerPanel
              className={blindCellClass(selectHighlight("tag", bigTag, ante.ante, activeMatches))}
            >
              <JimboText size="xs" tone="grey">
                Big
              </JimboText>
              <JamlTag tagName={bigTag} scale={0.75} />
            </JimboInnerPanel>
            <JimboInnerPanel
              className={blindCellClass(selectHighlight("boss", boss, ante.ante, activeMatches))}
            >
              <JimboText size="xs" tone="grey">
                Boss
              </JimboText>
              <JamlBoss bossName={boss} scale={0.75} />
            </JimboInnerPanel>
          </JimboRow>
        </JimboPanel>

        <JimboPanel title="Voucher" tone="gold">
          <JamlVoucher
            voucherName={voucher}
            scale={0.9}
            className={selectHighlight("voucher", voucher, ante.ante, activeMatches) ?? ""}
          />
        </JimboPanel>

        <JimboPanel title="Shop" tone="gold">
          <JimboRow wrap gap="sm" align="start">
            {ante.shopItems.map((item, i) => {
              const decoded = decodeMotelyItem(item);
              const highlight = decoded
                ? selectHighlight(
                    itemTypeOfCategory(decoded.category),
                    decoded.displayName,
                    ante.ante,
                    activeMatches
                  )
                : undefined;
              return <JamlyzerItemCard key={i} item={item} highlight={highlight} />;
            })}
          </JimboRow>
        </JimboPanel>

        {ante.packs.length > 0 && (
          <JimboPanel title="Packs" tone="gold">
            {ante.packs.map((pack, i) => (
              <JamlyzerPackSection key={i} pack={pack} ante={ante.ante} matches={activeMatches} />
            ))}
          </JimboPanel>
        )}

        <JamlyzerPulls ante={ante} matches={activeMatches} />

        {result.erraticDeck && result.erraticDeck.length > 0 && (
          <JamlyzerErraticDeck cards={result.erraticDeck} matches={activeMatches} />
        )}

        <JamlyzerEvents events={result.events} />
      </JimboStack>
    </JimboPanel>
  );
}
