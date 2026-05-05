"use client";
import React, { useState, useMemo } from "react";
import { JimboSprite } from "../../ui/sprites.js";
import { JimboColorOption, withAlpha } from "../../ui/tokens.js";
import { JimboText } from "../../ui/jimboText.js";
import { JOKERS, type SpriteEntry } from "../../sprites/spriteData.js";
import type { SlotSelection } from "./MysterySlot.js";

export type JokerRarity = "common" | "uncommon" | "rare" | "legendary";

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

function getJokerRarity(name: string): JokerRarity {
  if (LEGENDARY_JOKERS.has(name)) return "legendary";
  if (RARE_JOKERS.has(name)) return "rare";
  if (UNCOMMON_JOKERS.has(name)) return "uncommon";
  return "common";
}

function rarityToClauseKey(rarity: JokerRarity): string {
  if (rarity === "legendary") return "legendaryJoker";
  if (rarity === "rare") return "rareJoker";
  if (rarity === "uncommon") return "uncommonJoker";
  return "commonJoker";
}

const RARITY_DOT: Record<JokerRarity, string> = {
  legendary: JimboColorOption.PURPLE,
  rare:      JimboColorOption.RED,
  uncommon:  JimboColorOption.GREEN,
  common:    JimboColorOption.BLUE,
};

const C = JimboColorOption;

const LEGENDARY_LIST = JOKERS.filter((j) => LEGENDARY_JOKERS.has(j.name));
const NON_LEGENDARY = JOKERS.filter((j) => !LEGENDARY_JOKERS.has(j.name));

export interface JokerPickerProps {
  onSelect: (selection: SlotSelection) => void;
  onCancel: () => void;
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
    const rarity = getJokerRarity(joker.name);
    return (
      <div
        key={joker.name}
        className="j-juice-hover"
        onClick={() => handleSelect(joker)}
        title={joker.name}
        style={{
          display: "flex",
          flexDirection: "column",
          alignItems: "center",
          gap: 3,
          padding: 4,
          borderRadius: 4,
          cursor: "pointer",
          position: "relative",
        }}
      >
        <JimboSprite name={joker.name} sheet="Jokers" width={48} />
        <div
          style={{
            position: "absolute",
            top: 2,
            right: 2,
            width: 6,
            height: 6,
            borderRadius: "50%",
            background: RARITY_DOT[rarity],
            boxShadow: `0 0 0 1px ${withAlpha(C.BLACK, 0.5)}`,
          }}
        />
        <JimboText size="micro" tone="grey" style={{ maxWidth: 60, overflow: "hidden", textOverflow: "ellipsis", whiteSpace: "nowrap" }}>
          {joker.name}
        </JimboText>
      </div>
    );
  };

  return (
    <div style={{ display: "flex", flexDirection: "column", maxHeight: "80vh" }}>
      {/* Legendary row — always visible, no search needed */}
      <div style={{ padding: "8px 10px 6px", borderBottom: `1px solid ${withAlpha(C.PURPLE, 0.3)}` }}>
        <JimboText size="micro" tone="grey" style={{ marginBottom: 6, letterSpacing: 1 }}>Legendary</JimboText>
        <div style={{ display: "flex", gap: 4, justifyContent: "space-around" }}>
          {LEGENDARY_LIST.map(renderJoker)}
        </div>
      </div>

      {/* Search */}
      <div style={{ padding: "6px 10px 4px" }}>
        <input
          className="j-seed-input__field"
          type="text"
          placeholder="Search jokers..."
          value={search}
          onChange={(e) => setSearch(e.target.value)}
          style={{ fontSize: 13, padding: "6px 10px", textTransform: "none", letterSpacing: "0.04em", width: "100%" }}
        />
      </div>

      {/* Main grid */}
      <div className="hide-scrollbar" style={{
        display: "flex",
        flexWrap: "wrap",
        gap: 4,
        padding: "4px 10px 10px",
        overflowY: "auto",
        flex: 1,
        alignContent: "flex-start",
      }}>
        {filtered.map((j) => (
          <div key={j.name} style={{ width: 64 }}>{renderJoker(j)}</div>
        ))}
        {filtered.length === 0 && (
          <div style={{ width: "100%", padding: 20, textAlign: "center" }}>
            <JimboText size="sm" tone="grey">No jokers match "{search}"</JimboText>
          </div>
        )}
      </div>
    </div>
  );
}
