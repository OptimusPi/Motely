"use client";
import React, { useState, useMemo } from "react";
<<<<<<< HEAD
import { MotelyJokerRarity } from "motely-wasm";
import { JimboSprite } from "../../ui/sprites.js";
import { JimboText } from "../../ui/jimboText.js";
import { JimboTextInput } from "../../ui/JimboTextInput.js";
import { JOKERS, type SpriteEntry } from "../../sprites/spriteData.js";
import type { SlotSelection } from "./MysterySlot.js";

// JokerRarity is the motely-wasm enum — re-aliased for public-API stability.
export type JokerRarity = MotelyJokerRarity;

const LEGENDARY_JOKERS = new Set([
  "Canio", "Triboulet", "Yorick", "Chicot", "Perkeo",
]);

const RARE_JOKERS = new Set([
  "Blueprint", "Brainstorm", "Drivers License", "Burnt Joker",
  "Cartomancer", "Astronomer", "Satellite", "Shoot the Moon",
  "The Idol", "Seeing Double", "Matador", "Hit the Road",
  "The Duo", "The Trio", "The Family", "The Order", "The Tribe",
  "Stuntman", "Invisible Joker", "Showman", "Flower Pot",
  "Glass Joker", "Wee Joker", "Merry Andy", "Oops! All 6s",
  "Certificate", "Smeared Joker", "Throwback", "Hanging Chad",
  "Rough Gem", "Bloodstone", "Arrowhead", "Onyx Agate",
]);

const UNCOMMON_JOKERS = new Set([
  "Greedy Joker", "Lusty Joker", "Wrathful Joker", "Gluttonous Joker",
  "Jolly Joker", "Zany Joker", "Mad Joker", "Crazy Joker", "Droll Joker",
  "Sly Joker", "Wily Joker", "Clever Joker", "Devious Joker", "Crafty Joker",
  "Joker Stencil", "Four Fingers", "Mime", "Credit Card",
  "Ceremonial Dagger", "Banner", "Mystic Summit", "Marble Joker",
  "Loyalty Card", "8 Ball", "Misprint", "Dusk", "Raised Fist",
  "Fibonacci", "Steel Joker", "Scary Face", "Abstract Joker",
  "Delayed Gratification", "Hack", "Pareidolia", "Gros Michel",
  "Even Steven", "Odd Todd", "Scholar", "Business Card", "Supernova",
  "Ride the Bus", "Space Joker", "Egg", "Burglar", "Blackboard",
  "Runner", "Ice Cream", "DNA", "Splash", "Blue Joker",
  "Sixth Sense", "Constellation", "Hiker", "Faceless Joker",
  "Green Joker", "Superposition", "To Do List", "Cavendish",
  "Card Sharp", "Red Card", "Madness", "Square Joker",
  "Seance", "Riff-raff", "Vampire", "Shortcut",
  "Hologram", "Vagabond", "Baron", "Cloud 9", "Rocket", "Obelisk",
  "Midas Mask", "Luchador", "Photograph", "Gift Card", "Turtle Bean",
  "Erosion", "Reserved Parking", "Mail In Rebate", "To the Moon", "Hallucination",
  "Fortune Teller", "Golden Joker", "Lucky Cat", "Baseball Card", "Bull",
  "Diet Cola", "Trading Card", "Flash Card", "Popcorn",
  "Spare Trousers", "Ancient Joker", "Ramen", "Walkie Talkie",
  "Seltzer", "Castle", "Smiley Face", "Campfire",
  "Golden Ticket", "Mr. Bones", "Acrobat", "Sock and Buskin",
  "Swashbuckler", "Troubadour", "Bootstraps",
]);

function getJokerRarity(name: string): MotelyJokerRarity {
  if (LEGENDARY_JOKERS.has(name)) return MotelyJokerRarity.Legendary;
  if (RARE_JOKERS.has(name)) return MotelyJokerRarity.Rare;
  if (UNCOMMON_JOKERS.has(name)) return MotelyJokerRarity.Uncommon;
  return MotelyJokerRarity.Common;
}

function rarityToClauseKey(rarity: MotelyJokerRarity): string {
  switch (rarity) {
    case MotelyJokerRarity.Legendary: return "legendaryJoker";
    case MotelyJokerRarity.Rare:      return "rareJoker";
    case MotelyJokerRarity.Uncommon:  return "uncommonJoker";
    case MotelyJokerRarity.Common:    return "commonJoker";
    default:                          return "commonJoker";
  }
}

const LEGENDARY_LIST = JOKERS.filter((j) => LEGENDARY_JOKERS.has(j.name));
const NON_LEGENDARY = JOKERS.filter((j) => !LEGENDARY_JOKERS.has(j.name));
=======
import { MotelyJoker, MotelyJokerCommon, MotelyJokerUncommon, MotelyJokerRare } from "../../lib/motely/motelyCompatEnums.js";
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
>>>>>>> 4c1c0b639ac307d7366dccd1170ebadffbc2ab45

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
<<<<<<< HEAD
      <div
        key={joker.name}
        className="j-picker__item j-juice-hover"
=======
      <JimboPickerItem
        key={joker.name}
>>>>>>> 4c1c0b639ac307d7366dccd1170ebadffbc2ab45
        onClick={() => handleSelect(joker)}
        title={joker.name}
      >
        <JimboSprite name={joker.name} sheet="Jokers" width={48} />
        <JimboText size="micro" tone="white" className="j-picker__item-label">
          {joker.name}
        </JimboText>
<<<<<<< HEAD
      </div>
=======
      </JimboPickerItem>
>>>>>>> 4c1c0b639ac307d7366dccd1170ebadffbc2ab45
    );
  };

  return (
<<<<<<< HEAD
    <div className="j-picker">
      <div className="j-picker__section">
        <JimboText size="micro" tone="white" className="j-picker__section-title">Legendary</JimboText>
        <div className="j-picker__grid j-picker__grid--legendary">
          {LEGENDARY_LIST.map(renderJoker)}
        </div>
      </div>

      <div className="j-picker__search">
=======
    <JimboPicker>
      <JimboPickerSection>
        <JimboText size="micro" tone="white" className="j-picker__section-title">Legendary</JimboText>
        <JimboPickerGrid legendary>
          {LEGENDARY_LIST.map(renderJoker)}
        </JimboPickerGrid>
      </JimboPickerSection>

      <JimboPickerSearch>
>>>>>>> 4c1c0b639ac307d7366dccd1170ebadffbc2ab45
        <JimboTextInput
          className="j-picker__search-field"
          type="text"
          placeholder="Search jokers..."
          value={search}
          onChange={(e) => setSearch(e.target.value)}
        />
<<<<<<< HEAD
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
=======
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
>>>>>>> 4c1c0b639ac307d7366dccd1170ebadffbc2ab45
  );
}
