"use client";
import React, { useState, useMemo } from "react";
import { MotelyJokerRarity } from "motely-wasm/motely/enums";
import { JimboSprite } from "../../ui/sprites.js";
import { JimboText } from "../../ui/jimboText.js";
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
  }
}

const LEGENDARY_LIST = JOKERS.filter((j) => LEGENDARY_JOKERS.has(j.name));
const NON_LEGENDARY = JOKERS.filter((j) => !LEGENDARY_JOKERS.has(j.name));

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
        <input
          className="j-seed-input__field j-picker__search-field"
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
