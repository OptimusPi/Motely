"use client";
import React, { useState, useCallback, useMemo } from "react";
import { JimboSprite } from "../../ui/sprites.js";
import { JimboColorOption, withAlpha } from "../../ui/tokens.js";
import { JimboButton, type JimboTone } from "../../ui/panel.js";
import { JimboText } from "../../ui/jimboText.js";
import { JOKERS, type SpriteEntry } from "../../sprites/spriteData.js";
import { WILDCARD_SPRITES } from "../../sprites/spriteMapper.js";
import type { SlotSelection } from "./MysterySlot.js";

// ─── Rarity data ─────────────────────────────────────────────────────────────

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

// ─── Rarity → JimboButton tones ──────────────────────────────────────────────

const C = JimboColorOption;

interface RarityInfo {
  label: string;
  tone: JimboTone;
  hint: string;
}

const RARITY_META: Record<JokerRarity, RarityInfo> = {
  common: { label: "Common", tone: "blue", hint: "Found in shops and Buffoon Packs" },
  uncommon: { label: "Uncommon", tone: "green", hint: "Found in shops and Buffoon Packs" },
  rare: { label: "Rare", tone: "red", hint: "Found in shops and Buffoon Packs" },
  legendary: { label: "Legendary", tone: "tarot", hint: "Spawns from The Soul only!" },
};

// ─── Picker steps ────────────────────────────────────────────────────────────

type PickerStep = "rarity" | "specific";

export interface JokerPickerProps {
  onSelect: (selection: SlotSelection) => void;
  onCancel: () => void;
}

export function JokerPicker({ onSelect, onCancel }: JokerPickerProps) {
  const [step, setStep] = useState<PickerStep>("rarity");
  const [selectedRarity, setSelectedRarity] = useState<JokerRarity | null>(null);
  const [search, setSearch] = useState("");

  const filteredJokers = useMemo(() => {
    if (!selectedRarity) return [];
    return JOKERS.filter((j) => {
      if (getJokerRarity(j.name) !== selectedRarity) return false;
      if (search && !j.name.toLowerCase().includes(search.toLowerCase())) return false;
      return true;
    });
  }, [selectedRarity, search]);

  const handleRaritySelect = useCallback((rarity: JokerRarity) => {
    setSelectedRarity(rarity);
    setStep("specific");
    setSearch("");
  }, []);

  const handleJokerSelect = useCallback((joker: SpriteEntry) => {
    onSelect({
      category: "joker",
      value: joker.name,
      clauseKey: selectedRarity === "legendary" ? "legendaryJoker"
        : selectedRarity === "rare" ? "rareJoker"
          : selectedRarity === "uncommon" ? "uncommonJoker"
            : "commonJoker",
      rarity: selectedRarity ?? "common",
    });
  }, [onSelect, selectedRarity]);

  return (
    <div style={{ padding: 0, display: "flex", flexDirection: "column" }}>
      {/* Rarity step — stack of JimboButtons */}
      {step === "rarity" && (
        <div className="j-flex-col j-gap-sm" style={{ padding: 10 }}>
          {(["common", "uncommon", "rare", "legendary"] as JokerRarity[]).map((rarity) => {
            const meta = RARITY_META[rarity];
            return (
              <JimboButton
                key={rarity}
                tone={meta.tone}
                size="md"
                fullWidth
                onClick={() => handleRaritySelect(rarity)}
              >
                <span style={{ display: "flex", flexDirection: "column", gap: 2, textAlign: "left", width: "100%" }}>
                  <span>{meta.label}</span>
                  <span style={{ fontSize: 9, opacity: 0.7 }}>{meta.hint}</span>
                </span>
              </JimboButton>
            );
          })}
        </div>
      )}

      {/* Specific joker step */}
      {step === "specific" && selectedRarity && (
        <>
          {/* Header Action Row for Specific Joker */}
          <div className="j-flex j-items-center" style={{
            justifyContent: "space-between",
            padding: "8px 10px",
            borderBottom: `2px solid ${C.PANEL_EDGE}`,
          }}>
            <JimboButton tone="orange" size="xs" onClick={() => setStep("rarity")}>← Back</JimboButton>
            <JimboText size="md">{RARITY_META[selectedRarity].label} Jokers</JimboText>
            <div style={{ width: 44 }} />
          </div>

          <div className="j-flex j-gap-sm" style={{ padding: "8px 10px 4px" }}>
            <input
              className="j-seed-input__field"
              type="text"
              placeholder="Search jokers..."
              value={search}
              onChange={(e) => setSearch(e.target.value)}
              style={{ fontSize: 13, padding: "6px 10px", textTransform: "none", letterSpacing: "0.04em" }}
            />
          </div>

          {/* Legendary warning */}
          {selectedRarity === "legendary" && (
            <div className="j-inner-panel" style={{ margin: "4px 10px 6px", padding: "6px 10px" }}>
              <JimboText size="xs" tone="purple">
                Legendary jokers spawn from The Soul. Find it in Arcana Pack, Spectral Pack, Charm Tag, or Ethereal Tag only!
              </JimboText>
            </div>
          )}

          {/* Joker grid */}
          <div style={{
            display: "grid",
            gridTemplateColumns: "repeat(auto-fill, minmax(64px, 1fr))",
            gap: 6,
            padding: "8px 10px 10px"
          }}>
            {filteredJokers.map((joker) => (
              <div
                key={joker.name}
                onClick={() => handleJokerSelect(joker)}
                title={joker.name}
                style={{
                  display: "flex",
                  flexDirection: "column",
                  alignItems: "center",
                  gap: 3,
                  padding: 4,
                  borderRadius: 4,
                  cursor: "pointer",
                }}
              >
                <JimboSprite name={joker.name} sheet="Jokers" width={48} />
                <JimboText size="micro" tone="grey" style={{ maxWidth: 60, overflow: "hidden", textOverflow: "ellipsis", whiteSpace: "nowrap" }}>
                  {joker.name}
                </JimboText>
              </div>
            ))}
            {filteredJokers.length === 0 && (
              <div style={{ gridColumn: "1 / -1", padding: 20, textAlign: "center" }}>
                <JimboText size="sm" tone="grey">No jokers match "{search}"</JimboText>
              </div>
            )}
          </div>
        </>
      )}
    </div>
  );
}
