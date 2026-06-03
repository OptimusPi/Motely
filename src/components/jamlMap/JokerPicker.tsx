"use client";
import React, { useState, useMemo } from "react";
import { MotelyJoker, MotelyJokerCommon, MotelyJokerUncommon, MotelyJokerRare } from "motely-wasm";
import { JokerRarityTier } from "./jokerRarity.js";
import { JimboSprite } from "../../ui/sprites.js";
import { JimboText } from "../../ui/jimboText.js";
import { JimboTextInput } from "../../ui/JimboTextInput.js";
import { JOKERS, type SpriteEntry } from "../../sprites/spriteData.js";
import type { SlotSelection } from "./MysterySlot.js";

// JokerRarity re-aliases the local rarity tier — kept for public-API stability.
export type JokerRarity = JokerRarityTier;

// Rarity membership comes straight from motely-wasm's per-rarity joker enums, so
// it can never drift from the engine. Engine keys are PascalCase ids (e.g.
// "GreedyJoker"); our sprite names are spaced (e.g. "Greedy Joker"), so both are
// normalized to lowercase-alphanumeric before matching. One spelling the engine
// doesn't share: it writes "8 Ball" as "EightBall" — bridged explicitly below.
const normalizeJokerName = (name: string): string => name.toLowerCase().replace(/[^a-z0-9]/g, "");

const DISPLAY_NAME_ALIASES: Record<string, string> = {
  "8 Ball": "EightBall",
};

const enumNameKeys = (enumObj: object): string[] =>
  Object.keys(enumObj).filter((key) => Number.isNaN(Number(key)));

const normalizedKeySet = (enumObj: object): Set<string> =>
  new Set(enumNameKeys(enumObj).map(normalizeJokerName));

const UNCOMMON_KEYS = normalizedKeySet(MotelyJokerUncommon);
const RARE_KEYS = normalizedKeySet(MotelyJokerRare);
const COMMON_KEYS = normalizedKeySet(MotelyJokerCommon);
// Legendary jokers are the engine's full joker set minus the three named tiers.
const LEGENDARY_KEYS = new Set(
  enumNameKeys(MotelyJoker)
    .map(normalizeJokerName)
    .filter((key) => !COMMON_KEYS.has(key) && !UNCOMMON_KEYS.has(key) && !RARE_KEYS.has(key)),
);

const engineKey = (name: string): string =>
  normalizeJokerName(DISPLAY_NAME_ALIASES[name] ?? name);

function getJokerRarity(name: string): JokerRarityTier {
  const key = engineKey(name);
  if (LEGENDARY_KEYS.has(key)) return JokerRarityTier.Legendary;
  if (RARE_KEYS.has(key)) return JokerRarityTier.Rare;
  if (UNCOMMON_KEYS.has(key)) return JokerRarityTier.Uncommon;
  return JokerRarityTier.Common;
}

function rarityToClauseKey(rarity: JokerRarityTier): string {
  switch (rarity) {
    case JokerRarityTier.Legendary: return "legendaryJoker";
    case JokerRarityTier.Rare:      return "rareJoker";
    case JokerRarityTier.Uncommon:  return "uncommonJoker";
    case JokerRarityTier.Common:    return "commonJoker";
    default:                        return "commonJoker";
  }
}

const isLegendaryJoker = (joker: SpriteEntry): boolean => LEGENDARY_KEYS.has(engineKey(joker.name));
const LEGENDARY_LIST = JOKERS.filter(isLegendaryJoker);
const NON_LEGENDARY = JOKERS.filter((joker) => !isLegendaryJoker(joker));

export interface JokerPickerProps {
  onSelect: (selection: SlotSelection) => void;
  onCancel?: () => void;
}

export function JokerPicker({ onSelect }: JokerPickerProps) {
  const [search, setSearch] = useState("");

  const filtered = useMemo(() => {
    if (!search) return NON_LEGENDARY;
    const q = search.toLowerCase();
    return JOKERS.filter((j) => j.name.toLowerCase().includes(q));
  }, [search]);

  const handleSelect = (joker: SpriteEntry) => {
    const rarity = getJokerRarity(joker.name);
    onSelect({
      category: "joker",
      value: joker.name,
      clauseKey: rarityToClauseKey(rarity),
      rarity,
    });
  };

  const renderJoker = (joker: SpriteEntry) => {
    return (
      <div
        key={joker.name}
        className="j-picker__item j-juice-hover"
        onClick={() => handleSelect(joker)}
        title={joker.name}
      >
        <JimboSprite name={joker.name} sheet="Jokers" width={48} />
        <JimboText size="micro" tone="white" className="j-picker__item-label">
          {joker.name}
        </JimboText>
      </div>
    );
  };

  return (
    <div className="j-picker">
      <div className="j-picker__section">
        <JimboText size="micro" tone="white" className="j-picker__section-title">Legendary</JimboText>
        <div className="j-picker__grid j-picker__grid--legendary">
          {LEGENDARY_LIST.map(renderJoker)}
        </div>
      </div>

      <div className="j-picker__search">
        <JimboTextInput
          className="j-picker__search-field"
          type="text"
          placeholder="Search jokers..."
          value={search}
          onChange={(e) => setSearch(e.target.value)}
        />
      </div>

      <div className="j-picker__grid hide-scrollbar">
        {filtered.map(renderJoker)}
        {filtered.length === 0 && (
          <div className="j-picker__empty">
            <JimboText size="sm" tone="grey">No jokers match "{search}"</JimboText>
          </div>
        )}
      </div>
    </div>
  );
}
