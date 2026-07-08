"use client";

import React, { useMemo, useState } from "react";
import type {
  MotelyJamlyzerSeedResult,
  MotelyJamlyzerAnteResult,
  MotelyItem,
} from "motely-wasm";
import {
  MotelyBoosterPack,
  MotelyBossBlind,
  MotelyTag,
  MotelyVoucher,
  MotelyDeck,
  MotelyStake,
} from "motely-wasm";
import {
  JamlGameCard,
  JamlVoucher,
  JamlTag,
  JamlBoss,
} from "./GameCard.js";
import { StandardCard } from "./StandardCard.js";
import { decodeMotelyItemToJamlCard, decodeMotelyItem, type MotelyRenderableCategory } from "../decode/motelyItemDecoder.js";
import {
  parseJamlClauses,
  type ParsedJamlClause,
  type JamlItemType,
  matchClauseToAnte,
  highlightClassForKind,
  normalizeName,
} from "../lib/jaml/parseClauses.js";

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

function splitCamelCase(key: string): string {
  return key.replace(/([A-Z])/g, " $1").trim();
}

function packDisplayName(pack: MotelyBoosterPack): string {
  return splitCamelCase(MotelyBoosterPack[pack]);
}

function bossDisplayName(boss: MotelyBossBlind): string {
  return splitCamelCase(MotelyBossBlind[boss]);
}

function tagDisplayName(tag: MotelyTag): string {
  return splitCamelCase(MotelyTag[tag]);
}

function voucherDisplayName(voucher: MotelyVoucher): string {
  return splitCamelCase(MotelyVoucher[voucher]);
}

function deckDisplayName(deck: MotelyDeck): string {
  return splitCamelCase(MotelyDeck[deck]);
}

function stakeDisplayName(stake: MotelyStake): string {
  return splitCamelCase(MotelyStake[stake]);
}

function ItemCard({ item, scale = 0.85, highlight }: { item: MotelyItem; scale?: number; highlight?: string }) {
  const resolved = useMemo(() => decodeMotelyItemToJamlCard(item, scale), [item, scale]);
  if (!resolved) return <div className={`j-game-card j-game-card--unknown ${highlight || ""}`.trim()}>?</div>;
  return (
    <div className={highlight || undefined}>
      <JamlGameCard type={resolved.type} card={resolved.card} hoverTilt />
    </div>
  );
}

function itemTypeOfCategory(category: MotelyRenderableCategory): JamlItemType {
  switch (category) {
    case "playing":
      return "standardcard";
    case "joker":
      return "joker";
    case "tarot":
    case "planet":
    case "spectral":
      return category;
    case "consumable":
      return "consumable";
    default:
      return "unknown";
  }
}

function buildMatchMap(clauses: ParsedJamlClause[]): Map<string, ParsedJamlClause[]> {
  const map = new Map<string, ParsedJamlClause[]>();
  for (const clause of clauses) {
    for (const name of clause.names) {
      const key = `${clause.itemType}:${normalizeName(name)}`;
      const list = map.get(key) ?? [];
      list.push(clause);
      map.set(key, list);
    }
  }
  return map;
}

function selectHighlight(
  itemType: JamlItemType,
  name: string,
  ante: number,
  matches: Map<string, ParsedJamlClause[]>
): string | undefined {
  const key = `${itemType}:${normalizeName(name)}`;
  const clauses = matches.get(key) ?? [];
  const matching = clauses.filter((c) => matchClauseToAnte(c, ante));
  if (matching.length === 0) return undefined;
  // Prefer should, then must, then mustNot for the glow color.
  const primary = matching.find((c) => c.kind === "should") ?? matching.find((c) => c.kind === "must") ?? matching[0];
  return highlightClassForKind(primary.kind);
}

function PackSection({
  pack,
  ante,
  matches,
}: {
  pack: MotelyJamlyzerAnteResult["packs"][number];
  ante: number;
  matches: Map<string, ParsedJamlClause[]>;
}) {
  return (
    <div className="j-inner-panel" style={{ marginBottom: "var(--j-space-md)" }}>
      <div className="j-flex" style={{ alignItems: "center", gap: "var(--j-space-md)", marginBottom: "var(--j-space-sm)" }}>
        <span className="j-text j-text--body">{packDisplayName(pack.pack)}</span>
        <span className="j-badge j-badge--blue j-badge--sm">{pack.items.length} cards</span>
      </div>
      <div className="j-flex j-flex-wrap" style={{ gap: "var(--j-space-sm)" }}>
        {pack.items.map((item, i) => {
          const decoded = decodeMotelyItem(item);
          const highlight = decoded ? selectHighlight(itemTypeOfCategory(decoded.category), decoded.displayName, ante, matches) : undefined;
          return <ItemCard key={i} item={item} highlight={highlight} />;
        })}
      </div>
    </div>
  );
}

function PullsDrawer({
  ante,
  matches,
}: {
  ante: MotelyJamlyzerAnteResult;
  matches: Map<string, ParsedJamlClause[]>;
}) {
  const pulls = ante.pulls;
  const groups = [
    { title: "Judgement Jokers", items: pulls.judgementJokers },
    { title: "Wraith Jokers", items: pulls.wraithJokers },
    { title: "Emperor Tarots", items: pulls.emperorTarots },
    { title: "Purple Seal Tarots", items: pulls.purpleSealTarots },
    { title: "Sixth Sense Spectrals", items: pulls.sixthSenseSpectrals },
    { title: "Seance Spectrals", items: pulls.seanceSpectrals },
    { title: "Riff-Raff Jokers", items: pulls.riffRaffJokers },
    { title: "Rare Tag Jokers", items: pulls.rareTagJokers },
    { title: "Uncommon Tag Jokers", items: pulls.uncommonTagJokers },
    { title: "Legendary Jokers", items: pulls.legendaryJokers },
  ].filter((g) => g.items.length > 0);

  if (groups.length === 0 && pulls.voucherSequence.length === 0) return null;

  return (
    <div className="j-panel" style={{ gap: "var(--j-space-md)" }}>
      <div className="j-text j-text--heading j-text--upper j-text--gold">Pulls</div>
      {groups.map((group) => (
        <div key={group.title} className="j-inner-panel">
          <div className="j-text j-text--label j-text--grey" style={{ marginBottom: "var(--j-space-sm)" }}>
            {group.title}
          </div>
          <div className="j-flex j-flex-wrap" style={{ gap: "var(--j-space-sm)" }}>
            {group.items.map((item, i) => {
              const decoded = decodeMotelyItem(item);
              const highlight = decoded ? selectHighlight(itemTypeOfCategory(decoded.category), decoded.displayName, ante.ante, matches) : undefined;
              return <ItemCard key={i} item={item} highlight={highlight} />;
            })}
          </div>
        </div>
      ))}
      {pulls.voucherSequence.length > 0 && (
        <div className="j-inner-panel">
          <div className="j-text j-text--label j-text--grey" style={{ marginBottom: "var(--j-space-sm)" }}>
            Voucher Sequence
          </div>
          <div className="j-flex j-flex-wrap" style={{ gap: "var(--j-space-sm)" }}>
            {pulls.voucherSequence.map((voucher, i) => {
              const name = voucherDisplayName(voucher);
              const highlight = selectHighlight("voucher", name, ante.ante, matches);
              return (
                <div key={i} className={highlight || undefined}>
                  <JamlVoucher voucherName={name} scale={0.75} />
                </div>
              );
            })}
          </div>
        </div>
      )}
    </div>
  );
}

function EventsSection({ events }: { events: MotelyJamlyzerSeedResult["events"] }) {
  const rolls = [
    { key: "luckyMoney", label: "Lucky Money", values: events.luckyMoney },
    { key: "luckyMult", label: "Lucky Mult", values: events.luckyMult },
    { key: "cavendish", label: "Cavendish", values: events.cavendish },
    { key: "grosMichel", label: "Gros Michel", values: events.grosMichel },
    { key: "space", label: "Space Joker", values: events.space },
    { key: "business", label: "Business Card", values: events.business },
    { key: "bloodstone", label: "Bloodstone", values: events.bloodstone },
    { key: "parking", label: "Parking Meter", values: events.parking },
    { key: "eightBall", label: "Eight Ball", values: events.eightBall },
    { key: "glass", label: "Glass Joker", values: events.glass },
    { key: "omenGlobe", label: "Omen Globe", values: events.omenGlobe },
    { key: "theWheel", label: "The Wheel", values: events.theWheel },
  ].filter((r) => r.values && r.values.length > 0);

  if (rolls.length === 0) return null;

  return (
    <div className="j-panel" style={{ gap: "var(--j-space-md)" }}>
      <div className="j-text j-text--heading j-text--upper j-text--gold">Event Rolls</div>
      <div className="j-flex j-flex-wrap" style={{ gap: "var(--j-space-sm)" }}>
        {rolls.map((roll) => (
          <div key={roll.key} className="j-inner-panel" style={{ minWidth: 120 }}>
            <div className="j-text j-text--label j-text--grey">{roll.label}</div>
            <div className="j-text j-text--body">
              {roll.values.slice(0, 8).map((v, i) => (
                <span key={i} className="j-badge j-badge--grey j-badge--sm" style={{ marginRight: "var(--j-space-xs)" }}>
                  {String(v)}
                </span>
              ))}
              {roll.values.length > 8 && (
                <span className="j-text j-text--micro j-text--grey">+{roll.values.length - 8}</span>
              )}
            </div>
          </div>
        ))}
      </div>
    </div>
  );
}

function ErraticDeckPanel({
  cards,
  matches,
}: {
  cards: MotelyItem[];
  matches: Map<string, ParsedJamlClause[]>;
}) {
  if (cards.length === 0) return null;
  return (
    <div className="j-panel" style={{ gap: "var(--j-space-md)" }}>
      <div className="j-text j-text--heading j-text--upper j-text--gold">Erratic Deck</div>
      <div className="j-flex j-flex-wrap" style={{ gap: "var(--j-space-sm)" }}>
        {cards.map((item, i) => {
          const resolved = decodeMotelyItemToJamlCard(item, 0.65);
          if (resolved?.type === "playing") {
            const name = `${resolved.card.rank} of ${resolved.card.suit}`;
            const highlight = selectHighlight("standardcard", name, 0, matches);
            return (
              <div key={i} className={highlight || undefined}>
                <StandardCard
                  rank={resolved.card.rank as never}
                  suit={resolved.card.suit as never}
                  enhancement={resolved.card.enhancements?.[0] as never}
                  seal={resolved.card.seal as never}
                  edition={resolved.card.edition as never}
                  size={48}
                />
              </div>
            );
          }
          return <ItemCard key={i} item={item} scale={0.65} />;
        })}
      </div>
    </div>
  );
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

  const shouldClauses = useMemo(() => parsed.filter((c) => c.kind === "should"), [parsed]);
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
      <div className="j-panel j-text j-text--grey">
        No ante data available for seed {result.seed}.
      </div>
    );
  }

  return (
    <div className="j-panel" style={{ gap: "var(--j-space-lg)" }}>
      <div className="j-flex" style={{ gap: "var(--j-space-md)", alignItems: "flex-start" }}>
        <div className="j-ante-scroll" style={{ display: "flex", flexDirection: "column", gap: "var(--j-space-sm)", maxHeight: 420, overflowY: "auto", minWidth: 72 }}>
          {parsed.length > 0 && (
            <div className="j-panel" style={{ gap: "var(--j-space-sm)", padding: "var(--j-space-md)" }}>
              <div className="j-text j-text--label j-text--upper j-text--gold">Clauses</div>
              {shouldClauses.map((clause, i) => (
                <button
                  key={i}
                  className="j-btn j-btn--xs"
                  onMouseEnter={() => handleClauseHover(clause)}
                  onMouseLeave={() => handleClauseHover(null)}
                  style={{ width: "100%" }}
                >
                  <span className={`j-btn__face j-text j-text--micro ${tallies && i < tallies.length && tallies[i] > 0 ? "j-text--green" : "j-text--grey"}`.trim()}>
                    {tallies && i < tallies.length ? `${tallies[i]} · ` : ""}{clause.label}
                  </span>
                </button>
              ))}
              {parsed.filter((c) => c.kind !== "should").map((clause, i) => (
                <button
                  key={`other-${i}`}
                  className="j-btn j-btn--xs"
                  onMouseEnter={() => handleClauseHover(clause)}
                  onMouseLeave={() => handleClauseHover(null)}
                  style={{ width: "100%" }}
                >
                  <span className={`j-btn__face j-text j-text--micro ${clause.kind === "must" ? "j-text--red" : "j-text--grey"}`.trim()}>
                    {clause.kind === "must" ? "MUST " : "NOT "}{clause.label}
                  </span>
                </button>
              ))}
            </div>
          )}
          {anteNumbers.map((n) => (
            <button
              key={n}
              className="j-btn j-btn--xs"
              data-pressed={n === selectedAnte}
              onClick={() => setSelectedAnte(n)}
              style={{ width: "100%" }}
            >
              <span className="j-btn__face j-text j-text--label j-text--upper">
                Ante {n}
              </span>
            </button>
          ))}
        </div>

        <div className="j-flex-1 j-flex-col" style={{ gap: "var(--j-space-lg)", minWidth: 0 }}>
          <div className="j-panel" style={{ gap: "var(--j-space-sm)" }}>
            <div className="j-text j-text--xl j-text--gold">Seed: {result.seed}</div>
            <div className="j-flex j-flex-wrap" style={{ gap: "var(--j-space-md)" }}>
              <span className="j-text j-text--body j-text--grey">
                Score: <span className="j-text--gold">{result.score}</span>
              </span>
              {deck !== undefined && (
                <span className="j-text j-text--body j-text--grey">
                  Deck: <span className="j-text--white">{deckDisplayName(deck)}</span>
                </span>
              )}
              {stake !== undefined && (
                <span className="j-text j-text--body j-text--grey">
                  Stake: <span className="j-text--white">{stakeDisplayName(stake)}</span>
                </span>
              )}
            </div>
          </div>

          <div className="j-panel" style={{ gap: "var(--j-space-md)" }}>
            <div className="j-text j-text--heading j-text--upper j-text--gold">Blinds</div>
            <div className="j-flex j-flex-wrap" style={{ gap: "var(--j-space-md)", alignItems: "center" }}>
              <div className={`j-inner-panel j-flex ${selectHighlight("tag", tagDisplayName(ante.smallBlindTag), ante.ante, activeMatches) || ""}`.trim()} style={{ alignItems: "center", gap: "var(--j-space-md)" }}>
                <span className="j-text j-text--label j-text--grey">SMALL</span>
                <JamlTag tagName={tagDisplayName(ante.smallBlindTag)} scale={0.75} />
              </div>
              <div className={`j-inner-panel j-flex ${selectHighlight("tag", tagDisplayName(ante.bigBlindTag), ante.ante, activeMatches) || ""}`.trim()} style={{ alignItems: "center", gap: "var(--j-space-md)" }}>
                <span className="j-text j-text--label j-text--grey">BIG</span>
                <JamlTag tagName={tagDisplayName(ante.bigBlindTag)} scale={0.75} />
              </div>
              <div className={`j-inner-panel j-flex ${selectHighlight("boss", bossDisplayName(ante.boss), ante.ante, activeMatches) || ""}`.trim()} style={{ alignItems: "center", gap: "var(--j-space-md)" }}>
                <span className="j-text j-text--label j-text--grey">BOSS</span>
                <JamlBoss bossName={bossDisplayName(ante.boss)} scale={0.75} />
              </div>
            </div>
          </div>

          <div className="j-panel" style={{ gap: "var(--j-space-md)" }}>
            <div className="j-text j-text--heading j-text--upper j-text--gold">Voucher</div>
            <div className="j-flex j-flex-wrap" style={{ gap: "var(--j-space-sm)" }}>
              <div className={selectHighlight("voucher", voucherDisplayName(ante.voucher), ante.ante, activeMatches) || undefined}>
                <JamlVoucher voucherName={voucherDisplayName(ante.voucher)} scale={0.9} />
              </div>
            </div>
          </div>

          <div className="j-panel" style={{ gap: "var(--j-space-md)" }}>
            <div className="j-text j-text--heading j-text--upper j-text--gold">Shop</div>
            <div className="j-flex j-flex-wrap" style={{ gap: "var(--j-space-sm)" }}>
              {ante.shopItems.map((item, i) => {
                const decoded = decodeMotelyItem(item);
                const highlight = decoded ? selectHighlight(itemTypeOfCategory(decoded.category), decoded.displayName, ante.ante, activeMatches) : undefined;
                return <ItemCard key={i} item={item} highlight={highlight} />;
              })}
            </div>
          </div>

          {ante.packs.length > 0 && (
            <div className="j-panel" style={{ gap: "var(--j-space-md)" }}>
              <div className="j-text j-text--heading j-text--upper j-text--gold">Packs</div>
              {ante.packs.map((pack, i) => (
                <PackSection key={i} pack={pack} ante={ante.ante} matches={activeMatches} />
              ))}
            </div>
          )}

          <PullsDrawer ante={ante} matches={activeMatches} />
        </div>
      </div>

      {result.erraticDeck && result.erraticDeck.length > 0 && <ErraticDeckPanel cards={result.erraticDeck} matches={activeMatches} />}
      <EventsSection events={result.events} />
    </div>
  );
}
