"use client";
import React, { useState, useCallback, useMemo, useEffect } from "react";
import { MysterySlot, type SlotSelection, type JamlZone, type SlotCategory } from "./MysterySlot.js";
import { JokerPicker } from "./JokerPicker.js";
import {
  CategoryPicker,
  VOUCHER_PICKER_CONFIG,
  TAG_PICKER_CONFIG,
  BOSS_PICKER_CONFIG,
  TAROT_PICKER_CONFIG,
  PLANET_PICKER_CONFIG,
  SPECTRAL_PICKER_CONFIG,
  PACK_PICKER_CONFIG,
} from "./CategoryPicker.js";
import { JimboButton, JimboModal, type JimboTone } from "../../ui/panel.js";
import { JimboText } from "../../ui/jimboText.js";
import { JimboColorOption, withAlpha } from "../../ui/tokens.js";
import { JimboSprite } from "../../ui/sprites.js";
import { type SpriteSheetType } from "../../sprites/spriteMapper.js";

// ─── Types ───────────────────────────────────────────────────────────────────

export interface JamlMapEditorProps {
  /** Initial zone for the demo. */
  zone?: JamlZone;
  /** Callback when selections change. Returns JAML string. */
  onChange?: (jamlString: string) => void;
}

type PickerFlow = "category" | "joker" | SlotCategory;

interface ActiveSlot {
  ante: number;
  id: string;
  forceCategory?: SlotCategory;
}

export interface MapSlotSelection extends SlotSelection {
  zone: JamlZone;
}

type AnteSelections = Record<string, MapSlotSelection>;

// ─── Category menu items ─────────────────────────────────────────────────────

const C = JimboColorOption;

interface CategoryOption {
  key: SlotCategory;
  label: string;
  sprite: string;
  sheet: SpriteSheetType;
  tone: JimboTone;
  hint: string;
}

const CATEGORIES: CategoryOption[] = [
  { key: "joker",    label: "Joker",         sprite: "Joker",        sheet: "Jokers",   tone: "blue",     hint: "Shop, Buffoon Pack" },
  { key: "voucher",  label: "Voucher",       sprite: "Blank",        sheet: "Vouchers", tone: "orange",     hint: "1 per Ante in shop" },
  { key: "tarot",    label: "Tarot Card",    sprite: "The Fool",     sheet: "Tarots",   tone: "tarot",    hint: "Arcana Pack, shop" },
  { key: "planet",   label: "Planet Card",   sprite: "Mercury",      sheet: "Tarots",   tone: "planet",   hint: "Celestial Pack, shop" },
  { key: "spectral", label: "Spectral Card", sprite: "Grim",         sheet: "Tarots",   tone: "spectral", hint: "Ghost Deck, Spectral Pack" },
  { key: "tag",      label: "Tag",           sprite: "Uncommon Tag", sheet: "tags",     tone: "green",    hint: "Skip blind reward" },
  { key: "boss",     label: "Boss Blind",    sprite: "The Hook",     sheet: "BlindChips",tone: "red",      hint: "End of each Ante" },
  { key: "pack",     label: "Booster Pack",  sprite: "Arcana Pack",  sheet: "Boosters", tone: "orange",   hint: "Arcana, Celestial, etc." },
];

const ZONE_TONE: Record<JamlZone, JimboTone> = {
  must: "blue",
  should: "red",
  mustnot: "orange",
};

const ZONE_LABEL: Record<JamlZone, string> = {
  must: "Must",
  should: "Should",
  mustnot: "Must Not",
};

const CATEGORY_CONFIG_MAP: Record<SlotCategory, typeof VOUCHER_PICKER_CONFIG> = {
  joker:    VOUCHER_PICKER_CONFIG,
  voucher:  VOUCHER_PICKER_CONFIG,
  tag:      TAG_PICKER_CONFIG,
  boss:     BOSS_PICKER_CONFIG,
  tarot:    TAROT_PICKER_CONFIG,
  planet:   PLANET_PICKER_CONFIG,
  spectral: SPECTRAL_PICKER_CONFIG,
  pack:     PACK_PICKER_CONFIG,
};

// ─── Component ───────────────────────────────────────────────────────────────

export function JamlMapEditor({
  zone: initialZone = "must",
  onChange,
}: JamlMapEditorProps) {
  const [currentZone, setCurrentZone] = useState<JamlZone>(initialZone);
  const [ante, setAnte] = useState<number>(1);
  const [antesState, setAntesState] = useState<Record<number, AnteSelections>>({});
  const [activeSlot, setActiveSlot] = useState<ActiveSlot | null>(null);
  const [pickerFlow, setPickerFlow] = useState<PickerFlow>("category");

  const currentAnteSelections = antesState[ante] || {};

  const handleSlotTap = useCallback((anteIndex: number, id: string, forceCategory?: SlotCategory) => {
    setActiveSlot({ ante: anteIndex, id, forceCategory });
    setPickerFlow(forceCategory || "category");
  }, []);

  const handleSlotClear = useCallback((anteIndex: number, id: string) => {
    setAntesState((prev) => {
      const next = { ...prev };
      if (!next[anteIndex]) return next;
      const nextAnte = { ...next[anteIndex] };
      delete nextAnte[id];
      next[anteIndex] = nextAnte;
      return next;
    });
  }, []);

  const handleCategorySelect = useCallback((cat: SlotCategory) => {
    setPickerFlow(cat);
  }, []);

  const handleItemSelect = useCallback((selection: SlotSelection) => {
    if (!activeSlot) return;
    setAntesState((prev) => {
      const next = { ...prev };
      const nextAnte = { ...(next[activeSlot.ante] || {}) };
      nextAnte[activeSlot.id] = { ...selection, zone: currentZone };
      next[activeSlot.ante] = nextAnte;
      return next;
    });
    setActiveSlot(null);
  }, [activeSlot, currentZone]);

  const handlePickerCancel = useCallback(() => {
    if (activeSlot?.forceCategory) {
      setActiveSlot(null);
    } else if (pickerFlow !== "category") {
      setPickerFlow("category");
    } else {
      setActiveSlot(null);
    }
  }, [activeSlot, pickerFlow]);

  const handleOverlayClose = useCallback(() => {
    setActiveSlot(null);
  }, []);

  const jamlText = useMemo(() => buildJamlText(antesState), [antesState]);

  useEffect(() => {
    onChange?.(jamlText);
  }, [jamlText, onChange]);

  const renderSlot = (anteIndex: number, id: string, width: number, sheetType: SpriteSheetType, forceCategory?: SlotCategory) => {
    const sel = (antesState[anteIndex] || {})[id];
    return (
      <MysterySlot
        key={id}
        zone={sel ? sel.zone : currentZone}
        sheetType={sheetType}
        selection={sel}
        width={width}
        onTap={() => handleSlotTap(anteIndex, id, forceCategory)}
        onClear={sel ? () => handleSlotClear(anteIndex, id) : undefined}
        style={{ flexShrink: 0 }}
      />
    );
  };

  return (
    <div style={{ width: "100%", height: "100%", display: "flex", flexDirection: "column" }}>
      {/* Zone Toggle Header */}
      <div style={{ position: "sticky", top: 0, zIndex: 10, background: C.DARKEST, padding: "max(32px, env(safe-area-inset-top, 32px)) 0 8px 0", borderBottom: `2px solid ${C.PANEL_EDGE}` }}>
        <JimboText size="md" tone="white" style={{ textAlign: "center", marginBottom: 12 }}>JAML VISUAL BUILDER</JimboText>
        <div className="j-flex j-gap-sm" style={{ justifyContent: "center" }}>
          {(["must", "should", "mustnot"] as JamlZone[]).map((z) => (
            <JimboButton
              key={z}
              tone={currentZone === z ? ZONE_TONE[z] : "blue"}
              size="sm"
              onClick={() => setCurrentZone(z)}
              style={{ opacity: currentZone === z ? 1 : 0.4 }}
            >
              {ZONE_LABEL[z]}
            </JimboButton>
          ))}
        </div>
      </div>

      {/* Map Layout - Vertical Scrolling Antes */}
      <div className="hide-scrollbar" style={{
        flex: 1,
        overflowY: "auto",
        scrollSnapType: "y mandatory",
        scrollBehavior: "smooth"
      }}>
        {Array.from({ length: 40 }, (_, i) => i).map((a) => (
          <div key={a} style={{
            scrollSnapAlign: "start",
            padding: "24px 8px 64px 8px",
            minHeight: "100%", // ensuring each ante takes at least full viewport height to snap cleanly
            display: "flex",
            flexDirection: "column",
            gap: 24,
            borderBottom: `2px solid ${C.DARK_GREY}`
          }}>
            <JimboText size="md" tone="white" style={{ textAlign: "center", marginBottom: 8 }}>ANTE {a}</JimboText>
            
            {/* Row 1: Blinds & Tags & Voucher */}
            <div className="j-flex j-justify-between j-items-end">
              <div className="j-flex-col j-items-center j-gap-xs">
                <JimboText size="micro" tone="grey">VOUCHER</JimboText>
                {renderSlot(a, `ante_${a}_voucher`, 42, "Vouchers", "voucher")}
              </div>
              <div className="j-flex-col j-items-center j-gap-xs">
                <JimboText size="micro" tone="grey">SMALL</JimboText>
                {renderSlot(a, `ante_${a}_tag_small`, 42, "tags", "tag")}
              </div>
              <div className="j-flex-col j-items-center j-gap-xs">
                <JimboText size="micro" tone="grey">BIG</JimboText>
                {renderSlot(a, `ante_${a}_tag_big`, 42, "tags", "tag")}
              </div>
              <div className="j-flex-col j-items-center j-gap-xs">
                <JimboText size="micro" tone="grey">BOSS</JimboText>
                {renderSlot(a, `ante_${a}_boss`, 42, "BlindChips", "boss")}
              </div>
            </div>

            {/* Row 2: Shop Items */}
            <div className="j-flex-col j-gap-xs">
              <JimboText size="xs" tone="grey" style={{ letterSpacing: 1 }}>SHOP ITEMS</JimboText>
              <div className="j-flex hide-scrollbar j-gap-sm" style={{ overflowX: "auto", paddingBottom: 8 }}>
                {[1,2,3,4,5,6,7,8].map(i => renderSlot(a, `ante_${a}_shop_${i}`, 52, "Jokers"))}
              </div>
            </div>

            {/* Row 3: Packs */}
            <div className="j-flex-col j-gap-xs">
              <JimboText size="xs" tone="grey" style={{ letterSpacing: 1 }}>PACKS</JimboText>
              <div className="j-flex j-gap-sm" style={{ flexWrap: "wrap" }}>
                {[1,2,3,4,5,6].map(i => renderSlot(a, `ante_${a}_pack_${i}`, 64, "Boosters", "pack"))}
              </div>
            </div>
          </div>
        ))}
      </div>

      {/* Picker overlay */}
      <JimboModal
        open={activeSlot !== null}
        onClose={handlePickerCancel}
        title={pickerFlow === "category" ? "Select Category" : undefined}
        className="j-picker-modal"
      >
        {activeSlot !== null && (
          pickerFlow === "category" ? (
            <CategoryMenu onSelect={handleCategorySelect} />
          ) : pickerFlow === "joker" ? (
            <JokerPicker
              onSelect={handleItemSelect}
              onCancel={handlePickerCancel}
            />
          ) : (
            <CategoryPicker
              config={CATEGORY_CONFIG_MAP[pickerFlow as SlotCategory]}
              onSelect={handleItemSelect}
              onCancel={handlePickerCancel}
            />
          )
        )}
      </JimboModal>
    </div>
  );
}

// ─── Category Selection Menu ─────────────────────────────────────────────────

function CategoryMenu({
  onSelect,
}: {
  onSelect: (cat: SlotCategory) => void;
}) {
  return (
    <div className="hide-scrollbar" style={{
      display: "flex",
      flexDirection: "column",
      gap: 12,
      padding: "10px 4px",
      maxHeight: "70vh",
      overflowY: "auto",
    }}>
      {CATEGORIES.map((cat) => (
        <JimboButton
          key={cat.key}
          tone={cat.tone}
          size="sm"
          fullWidth
          onClick={() => onSelect(cat.key)}
        >
          <div style={{ display: "flex", alignItems: "center", gap: 8, width: "100%", textAlign: "left" }}>
            <JimboSprite name={cat.sprite} sheet={cat.sheet} width={24} />
            <div style={{ display: "flex", flexDirection: "column", gap: 1 }}>
              <span style={{ fontSize: 11 }}>{cat.label}</span>
              <span style={{ fontSize: 8, opacity: 0.7, letterSpacing: "0.04em", lineHeight: 1, whiteSpace: "normal" }}>{cat.hint}</span>
            </div>
          </div>
        </JimboButton>
      ))}
    </div>
  );
}

// ─── Build JAML text from slots ──────────────────────────────────────────────

function buildJamlText(antes: Record<number, AnteSelections>): string {
  const byZone: Record<JamlZone, Record<string, { value: string; antes: number[] }[]>> = {
    must: {}, should: {}, mustnot: {}
  };

  for (const [anteStr, selections] of Object.entries(antes)) {
    const anteNum = parseInt(anteStr, 10);
    for (const sel of Object.values(selections)) {
      const zone = sel.zone;
      const key = sel.clauseKey;
      
      if (!byZone[zone][key]) {
        byZone[zone][key] = [];
      }
      
      const existing = byZone[zone][key].find(item => item.value === sel.value);
      if (existing) {
        if (!existing.antes.includes(anteNum)) existing.antes.push(anteNum);
      } else {
        byZone[zone][key].push({ value: sel.value, antes: [anteNum] });
      }
    }
  }

  let lines: string[] = [];
  lines.push("name: My Custom Seed Map");
  lines.push("author: JamlBuilder");
  lines.push("description: Auto-generated from the visual editor.");
  lines.push("deck: Red");
  lines.push("stake: White");

  for (const [zone, label] of [["must", "must"], ["should", "should"], ["mustnot", "mustNot"]] as const) {
    const clauses = byZone[zone as JamlZone];
    if (Object.keys(clauses).length === 0) continue;
    
    lines.push(`${label}:`);
    for (const [key, items] of Object.entries(clauses)) {
      for (const item of items) {
        lines.push(`  - ${key}: ${item.value}`);
        // Only emit `antes:` if it's not all 8 antes (simplification, or just emit it)
        if (item.antes.length < 8) {
          lines.push(`    antes: [${item.antes.sort((a,b)=>a-b).join(", ")}]`);
        }
      }
    }
  }

  return lines.join("\n") + "\n";
}
