"use client";
/* eslint-disable react-refresh/only-export-components */
import React, { useState, useCallback, useMemo } from "react";
import { JimboSprite } from "../../ui/sprites.js";
import { JimboColorOption } from "../../ui/tokens.js";
import { JimboText } from "../../ui/jimboText.js";
import { JimboTextInput } from "../../ui/JimboTextInput.js";
import {
  JimboPicker,
  JimboPickerGrid,
  JimboPickerItem,
  JimboPickerSearch,
  JimboPickerEmpty,
  JimboPickerHint,
  JimboPickerPair,
} from "../../ui/JimboPicker.js";
import type { SpriteEntry } from "../../sprites/spriteData.js";
import type { SpriteSheetType } from "../../sprites/spriteMapper.js";
import type { SlotSelection, SlotCategory } from "./MysterySlot.js";

// ─── Config per category ─────────────────────────────────────────────────────

export interface CategoryPickerConfig {
  /** Display title, e.g. "Vouchers" */
  title: string;
  /** The SlotCategory value */
  category: SlotCategory;
  /** JAML clause key emitted on select, e.g. "voucher" */
  clauseKey: string;
  /** Which sprite sheet to render from */
  sheet: SpriteSheetType;
  /** Full list of items for the grid */
  items: SpriteEntry[];
  /** Accent color for the header/buttons */
  accent: string;
  /** Optional tooltip hint shown in the "Any" button area */
  hint?: string;
}

export interface CategoryPickerProps {
  config: CategoryPickerConfig;
  onSelect: (selection: SlotSelection) => void;
  onCancel?: () => void;
}

const C = JimboColorOption;

// ─── Component ───────────────────────────────────────────────────────────────

export function CategoryPicker({ config, onSelect }: CategoryPickerProps) {
  const [search, setSearch] = useState("");

  const filtered = useMemo(() => {
    if (!search) return config.items;
    const q = search.toLowerCase();
    return config.items.filter((item) => item.name.toLowerCase().includes(q));
  }, [config.items, search]);

  const pairedVouchers = useMemo(() => {
    if (config.category !== "voucher") return null;
    const bases = config.items.filter((item) => item.pos.y % 2 === 0);
    const pairs = bases.map((base) => {
      const upgrade = config.items.find((u) => u.pos.x === base.pos.x && u.pos.y === base.pos.y + 1);
      return { base, upgrade };
    });
    if (!search) return pairs;
    const q = search.toLowerCase();
    return pairs.filter(p => p.base.name.toLowerCase().includes(q) || p.upgrade?.name.toLowerCase().includes(q));
  }, [config.items, search, config.category]);

  const handleSelect = useCallback(
    (item: SpriteEntry) => {
      onSelect({
        category: config.category,
        value: item.name,
        clauseKey: config.clauseKey,
      });
    },
    [onSelect, config],
  );

  const renderItem = (item: SpriteEntry, isMuted = false) => (
    <JimboPickerItem
      key={item.name}
      onClick={() => handleSelect(item)}
      title={item.name}
      muted={isMuted}
    >
      <JimboSprite name={item.name} sheet={config.sheet} width={48} />
      <JimboText size="micro" tone="white" className="j-picker__item-label">
        {item.name}
      </JimboText>
    </JimboPickerItem>
  );

  return (
    <JimboPicker>
      <JimboPickerSearch>
        <JimboTextInput
          className="j-picker__search-field"
          type="text"
          placeholder={`Search ${config.title.toLowerCase()}`}
          value={search}
          onChange={(e) => setSearch(e.target.value)}
        />
      </JimboPickerSearch>

      {config.hint && (
        <JimboPickerHint>
          <JimboText size="xs" tone="grey">
            {config.hint}
          </JimboText>
        </JimboPickerHint>
      )}

      <JimboPickerGrid scroll>
        {config.category === "voucher" && pairedVouchers ? (
          pairedVouchers.map((pair) => (
            <JimboPickerPair key={pair.base.name}>
              {renderItem(pair.base, search ? !pair.base.name.toLowerCase().includes(search.toLowerCase()) : false)}
              {pair.upgrade && renderItem(pair.upgrade, search ? !pair.upgrade.name.toLowerCase().includes(search.toLowerCase()) : false)}
            </JimboPickerPair>
          ))
        ) : (
          filtered.map((item) => renderItem(item))
        )}

        {((config.category === "voucher" && pairedVouchers?.length === 0) ||
          (config.category !== "voucher" && filtered.length === 0)) && (
            <JimboPickerEmpty>
              <JimboText size="sm" tone="grey">No matches for "{search}"</JimboText>
            </JimboPickerEmpty>
          )}
      </JimboPickerGrid>
    </JimboPicker>
  );
}

// ─── Pre-built configs ───────────────────────────────────────────────────────

import { VOUCHERS, TAGS, BOSSES, BOOSTER_PACKS, TAROTS_AND_PLANETS } from "../../sprites/spriteData.js";

// Split consumables by type
const TAROT_CARDS = TAROTS_AND_PLANETS.filter((c) => {
  const y = c.pos.y;
  return y <= 2 && c.name !== "The Soul" && c.name !== "Black Hole";
}).filter((c) => {
  return c.pos.y <= 1 || (c.pos.y === 2 && c.pos.x <= 1);
});

const PLANET_CARDS = TAROTS_AND_PLANETS.filter((c) => {
  return c.pos.y === 3 ||
    c.name === "Planet X" || c.name === "Ceres" || c.name === "Eris" ||
    c.name === "Black Hole";
});

const SPECTRAL_CARDS = TAROTS_AND_PLANETS.filter((c) => {
  return c.pos.y >= 4 || c.name === "The Soul";
});

export const VOUCHER_PICKER_CONFIG: CategoryPickerConfig = {
  title: "Vouchers",
  category: "voucher",
  clauseKey: "voucher",
  sheet: "Vouchers",
  items: VOUCHERS,
  accent: C.ORANGE,
};

export const TAG_PICKER_CONFIG: CategoryPickerConfig = {
  title: "Tags",
  category: "tag",
  clauseKey: "tag",
  sheet: "tags",
  items: TAGS,
  accent: C.GREEN,
};

export const BOSS_PICKER_CONFIG: CategoryPickerConfig = {
  title: "Boss Blinds",
  category: "boss",
  clauseKey: "boss",
  sheet: "BlindChips",
  items: BOSSES,
  accent: C.RED,
  hint: "Boss Blinds appear at the end of each Ante.",
};

export const TAROT_PICKER_CONFIG: CategoryPickerConfig = {
  title: "Tarot Cards",
  category: "tarot",
  clauseKey: "tarotCard",
  sheet: "Tarots",
  items: TAROT_CARDS,
  accent: C.PURPLE,
  hint: "Found in Arcana Packs and shops.",
};

export const PLANET_PICKER_CONFIG: CategoryPickerConfig = {
  title: "Planet Cards",
  category: "planet",
  clauseKey: "planetCard",
  sheet: "Tarots",
  items: PLANET_CARDS,
  accent: C.BLUE,
  hint: "Found in Celestial Packs and shops.",
};

export const SPECTRAL_PICKER_CONFIG: CategoryPickerConfig = {
  title: "Spectral Cards",
  category: "spectral",
  clauseKey: "spectralCard",
  sheet: "Tarots",
  items: SPECTRAL_CARDS,
  accent: C.TEAL_GREY,
  hint: "Found in Spectral Packs. Ghost Deck only for shop spawns!",
};

export const PACK_PICKER_CONFIG: CategoryPickerConfig = {
  title: "Booster Packs",
  category: "pack",
  clauseKey: "pack",
  sheet: "Boosters",
  items: BOOSTER_PACKS,
  accent: C.ORANGE,
};
