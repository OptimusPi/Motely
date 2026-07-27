"use client";
import React, { useState, useMemo } from "react";
import { Vocab } from "jaml-lang";
import { JokerRarityTier } from "./jokerRarity.js";
import { JimboSprite } from "../../ui/sprites.js";
import { JimboText } from "../../ui/jimboText.js";
import { JimboTextInput } from "../../ui/JimboTextInput.js";
import {
  JimboPicker,
  JimboPickerSection,
  JimboPickerGrid,
  JimboPickerItem,
  JimboPickerSearch,
  JimboPickerEmpty,
} from "../../ui/JimboPicker.js";
import { JOKERS, type SpriteEntry } from "../../sprites/spriteData.js";
import type { SlotSelection } from "./MysterySlot.js";

// JokerRarity re-aliases the local rarity tier — kept for public-API stability.
export type JokerRarity = JokerRarityTier;

// Rarity membership comes straight from jaml-lang's generated vocab — itself
// generated from the Motely engine — so it can never drift from the engine.
// Engine keys are PascalCase ids (e.g. "GreedyJoker"); our sprite names are
// spaced (e.g. "Greedy Joker"), so both are normalized to lowercase-
// alphanumeric before matching. One spelling the engine doesn't share: it
// writes "8 Ball" as "EightBall" — bridged explicitly below.
const normalizeJokerName = (name: string): string => name.toLowerCase().replace(/[^a-z0-9]/g, "");

const DISPLAY_NAME_ALIASES: Record<string, string> = {
  "8 Ball": "EightBall",
};

const normalizedKeySet = (names: readonly string[]): Set<string> =>
  new Set(names.map(normalizeJokerName));

const UNCOMMON_KEYS = normalizedKeySet(Vocab.Enums.MotelyJokerUncommon);
const RARE_KEYS = normalizedKeySet(Vocab.Enums.MotelyJokerRare);
const COMMON_KEYS = normalizedKeySet(Vocab.Enums.MotelyJokerCommon);
// Legendary jokers are the engine's full joker set minus the three named tiers.
const LEGENDARY_KEYS = new Set(
  Vocab.Enums.MotelyJoker.map(normalizeJokerName)
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
      <JimboPickerItem
        key={joker.name}
        onClick={() => handleSelect(joker)}
        title={joker.name}
      >
        <JimboSprite name={joker.name} sheet="Jokers" width={48} />
        <JimboText size="micro" tone="white" className="j-picker__item-label">
          {joker.name}
        </JimboText>
      </JimboPickerItem>
    );
  };

  return (
    <JimboPicker>
      <JimboPickerSection>
        <JimboText size="micro" tone="white" className="j-picker__section-title">Legendary</JimboText>
        <JimboPickerGrid legendary>
          {LEGENDARY_LIST.map(renderJoker)}
        </JimboPickerGrid>
      </JimboPickerSection>

      <JimboPickerSearch>
        <JimboTextInput
          className="j-picker__search-field"
          type="text"
          placeholder="Search jokers..."
          value={search}
          onChange={(e) => setSearch(e.target.value)}
        />
      </JimboPickerSearch>

      <JimboPickerGrid scroll>
        {filtered.map(renderJoker)}
        {filtered.length === 0 && (
          <JimboPickerEmpty>
            <JimboText size="sm" tone="grey">No jokers match "{search}"</JimboText>
          </JimboPickerEmpty>
        )}
      </JimboPickerGrid>
    </JimboPicker>
  );
}
