"use client";
import React, { useState, useCallback, useRef, useEffect } from "react";
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

type PickerFlow = "category" | "joker" | SlotCategory | "packUnsupported";

interface ActiveSlot {
  ante: number;
  id: string;
  forceCategory?: SlotCategory;
}

interface ActivePackSelection {
  packName: string;
  slotIndex: number;
}

interface PackDetailState {
  ante: number;
  id: string;
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
  { key: "joker", label: "Joker", sprite: "Joker", sheet: "Jokers", tone: "blue", hint: "Shop, Buffoon Pack" },
  { key: "voucher", label: "Voucher", sprite: "Blank", sheet: "Vouchers", tone: "orange", hint: "1 per Ante in shop" },
  { key: "tarot", label: "Tarot Card", sprite: "The Fool", sheet: "Tarots", tone: "tarot", hint: "Arcana Pack, shop" },
  { key: "planet", label: "Planet Card", sprite: "Mercury", sheet: "Tarots", tone: "planet", hint: "Celestial Pack, shop" },
  { key: "spectral", label: "Spectral Card", sprite: "Grim", sheet: "Tarots", tone: "spectral", hint: "Ghost Deck, Spectral Pack" },
  { key: "tag", label: "Tag", sprite: "Uncommon Tag", sheet: "tags", tone: "green", hint: "Skip blind reward" },
  { key: "boss", label: "Boss Blind", sprite: "The Hook", sheet: "BlindChips", tone: "red", hint: "End of each Ante" },
  { key: "pack", label: "Booster Pack", sprite: "Arcana Pack", sheet: "Boosters", tone: "orange", hint: "Arcana, Celestial, etc." },
];

const ZONE_TONE: Record<JamlZone, JimboTone> = {
  must: "blue",
  should: "green",
  mustnot: "red",
};

const ZONE_LABEL: Record<JamlZone, string> = {
  must: "Required",
  should: "Bonus",
  mustnot: "Avoid",
};

const CATEGORY_CONFIG_MAP: Record<SlotCategory, typeof VOUCHER_PICKER_CONFIG> = {
  joker: VOUCHER_PICKER_CONFIG,
  voucher: VOUCHER_PICKER_CONFIG,
  tag: TAG_PICKER_CONFIG,
  boss: BOSS_PICKER_CONFIG,
  tarot: TAROT_PICKER_CONFIG,
  planet: PLANET_PICKER_CONFIG,
  spectral: SPECTRAL_PICKER_CONFIG,
  pack: PACK_PICKER_CONFIG,
};

// ─── Component ───────────────────────────────────────────────────────────────

export function JamlMapEditor({
  zone: initialZone = "must",
  onChange,
}: JamlMapEditorProps) {
  const onChangeRef = useRef(onChange);
  useEffect(() => {
    onChangeRef.current = onChange;
  });
  const [currentZone, setCurrentZone] = useState<JamlZone>(initialZone);
  const [antesState, setAntesState] = useState<Record<number, AnteSelections>>({});
  const [activeSlot, setActiveSlot] = useState<ActiveSlot | null>(null);
  const [activePackDetail, setActivePackDetail] = useState<PackDetailState | null>(null);
  const [pickerFlow, setPickerFlow] = useState<PickerFlow>("category");
  const [activePackSelection, setActivePackSelection] = useState<ActivePackSelection | null>(null);

  const handleSlotTap = useCallback((anteIndex: number, id: string, forceCategory?: SlotCategory) => {
    const existing = (antesState[anteIndex] || {})[id];
    if (forceCategory === "pack" && existing?.packName) {
      setActivePackDetail({ ante: anteIndex, id });
      return;
    }
    setActiveSlot({ ante: anteIndex, id, forceCategory });
    setPickerFlow(forceCategory || "category");
  }, [antesState]);

  const handleSlotClear = useCallback((anteIndex: number, id: string) => {
    setAntesState((prev) => {
      const next = { ...prev };
      if (!next[anteIndex]) return next;
      const nextAnte = { ...next[anteIndex] };
      delete nextAnte[id];
      next[anteIndex] = nextAnte;
      onChangeRef.current?.(buildJamlText(next));
      return next;
    });
    setActivePackDetail((prev) => prev && prev.ante === anteIndex && prev.id === id ? null : prev);
  }, []);

  const handlePackChange = useCallback(() => {
    if (!activePackDetail) return;
    setActivePackDetail(null);
    setActivePackSelection(null);
    setActiveSlot({ ante: activePackDetail.ante, id: activePackDetail.id, forceCategory: "pack" });
    setPickerFlow("pack");
  }, [activePackDetail]);

  const handleCategorySelect = useCallback((cat: SlotCategory) => {
    setPickerFlow(cat);
  }, []);

  const handleItemSelect = useCallback((selection: SlotSelection) => {
    if (!activeSlot) return;

    if (activeSlot.forceCategory === "pack" && selection.category === "pack") {
      const slotIndex = getPackSlotIndex(activeSlot.id);
      if (slotIndex === null) return;
      const nextFlow = getPackFollowupFlow(selection.value);
      setActivePackSelection({ packName: selection.value, slotIndex });
      setPickerFlow(nextFlow);
      return;
    }

    const finalSelection = activePackSelection
      ? { ...selection, packName: activePackSelection.packName, boosterPacks: [activePackSelection.slotIndex] }
      : selection;

    setAntesState((prev) => {
      const next = { ...prev };
      const nextAnte = { ...(next[activeSlot.ante] || {}) };
      nextAnte[activeSlot.id] = { ...finalSelection, zone: currentZone };
      next[activeSlot.ante] = nextAnte;
      onChangeRef.current?.(buildJamlText(next));
      return next;
    });
    setActivePackSelection(null);
    setActiveSlot(null);
  }, [activePackSelection, activeSlot, currentZone]);

  const handlePickerCancel = useCallback(() => {
    if (activeSlot?.forceCategory === "pack" && activePackSelection) {
      setActivePackSelection(null);
      setPickerFlow("pack");
    } else if (activeSlot?.forceCategory) {
      setActiveSlot(null);
    } else if (pickerFlow !== "category") {
      setPickerFlow("category");
    } else {
      setActiveSlot(null);
    }
  }, [activePackSelection, activeSlot, pickerFlow]);

  const handleOverlayClose = useCallback(() => {
    setActivePackSelection(null);
    setActiveSlot(null);
    setActivePackDetail(null);
  }, []);


  const activePackDetailSelection = activePackDetail
    ? (antesState[activePackDetail.ante] || {})[activePackDetail.id]
    : undefined;

  const handleScrollAttach = useCallback((node: HTMLDivElement | null) => {
    if (!node) return;
    const firstChild = node.children[1] as HTMLElement | undefined;
    if (firstChild) node.scrollTop = firstChild.offsetTop;
  }, []);

  const renderSlot = (anteIndex: number, id: string, width: number, sheetType: SpriteSheetType, forceCategory?: SlotCategory) => {
    const sel = (antesState[anteIndex] || {})[id];
    return (
      <MysterySlot
        key={id}
        zone={sel ? sel.zone : "must"}
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
      <div ref={handleScrollAttach} className="hide-scrollbar" style={{
        flex: 1,
        overflowY: "scroll",
        overflowX: "hidden",
        scrollSnapType: "y mandatory",
        scrollBehavior: "smooth",
        WebkitOverflowScrolling: "touch",
        overscrollBehaviorY: "contain",
      }}>
        {Array.from({ length: 40 }, (_, i) => i).map((a) => (
          <div key={a} style={{
            scrollSnapAlign: "start",
            scrollSnapStop: "always",
            padding: "24px 8px 64px 8px",
            minHeight: "100%",
            boxSizing: "border-box",
            display: "flex",
            flexDirection: "column",
            gap: 24,
            borderBottom: `2px solid ${C.DARK_GREY}`
          }}>
            <JimboText size="md" tone="white" style={{ textAlign: "center", marginBottom: 8 }}>Ante {a}</JimboText>

            {/* Row 1: Blinds & Tags & Voucher */}
            <div className="j-flex j-justify-between j-items-end">
              <div className="j-flex-col j-items-center j-gap-xs">
                <JimboText size="micro" tone="grey">Voucher</JimboText>
                {renderSlot(a, `ante_${a}_voucher`, 42, "Vouchers", "voucher")}
              </div>
              <div className="j-flex-col j-items-center j-gap-xs">
                <JimboText size="micro" tone="grey">Small</JimboText>
                {renderSlot(a, `ante_${a}_tag_small`, 42, "tags", "tag")}
              </div>
              <div className="j-flex-col j-items-center j-gap-xs">
                <JimboText size="micro" tone="grey">Big</JimboText>
                {renderSlot(a, `ante_${a}_tag_big`, 42, "tags", "tag")}
              </div>
              <div className="j-flex-col j-items-center j-gap-xs">
                <JimboText size="micro" tone="grey">Boss</JimboText>
                {renderSlot(a, `ante_${a}_boss`, 42, "BlindChips", "boss")}
              </div>
            </div>

            {/* Row 2: Shop Items */}
            <div className="j-flex-col j-gap-xs">
              <JimboText size="xs" tone="grey" style={{ letterSpacing: 1 }}>Shop Items</JimboText>
              <div className="j-flex hide-scrollbar j-gap-sm" style={{ overflowX: "auto", paddingBottom: 8 }}>
                {[1, 2, 3, 4, 5, 6, 7, 8].map(i => renderSlot(a, `ante_${a}_shop_${i}`, 52, "Jokers"))}
              </div>
            </div>

            {/* Row 3: Packs */}
            <div className="j-flex-col j-gap-xs">
              <JimboText size="xs" tone="grey" style={{ letterSpacing: 1 }}>Packs</JimboText>
              <div className="j-flex-col j-gap-sm">
                {getPackRows(a).map((row, rowIndex) => (
                  <div key={rowIndex} className="j-flex j-gap-sm" style={{ flexWrap: "nowrap" }}>
                    {row.map(i => renderSlot(a, `ante_${a}_pack_${i}`, 64, "Boosters", "pack"))}
                  </div>
                ))}
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
          <div style={{ display: "flex", flexDirection: "column", gap: 8 }}>
            <div className="j-inner-panel" style={{ padding: "8px 10px", marginBottom: 2 }}>
              <JimboText size="xs" tone="grey" style={{ display: "block", marginBottom: 6 }}>
                This pick will be added as
              </JimboText>
              <div className="j-flex j-gap-sm" style={{ flexWrap: "wrap" }}>
                {(["must", "should", "mustnot"] as JamlZone[]).map((z) => (
                  <JimboButton
                    key={z}
                    tone={currentZone === z ? ZONE_TONE[z] : "grey"}
                    size="xs"
                    onClick={() => setCurrentZone(z)}
                  >
                    {ZONE_LABEL[z]}
                  </JimboButton>
                ))}
              </div>
            </div>

            {pickerFlow === "category" ? (
              <CategoryMenu onSelect={handleCategorySelect} />
            ) : pickerFlow === "joker" ? (
              <JokerPicker
                onSelect={handleItemSelect}
              />
            ) : pickerFlow === "packUnsupported" ? (
              <div className="j-flex-col j-gap-sm" style={{ padding: 10, maxWidth: 360 }}>
                <div className="j-inner-panel" style={{ padding: "10px 12px" }}>
                  <JimboText size="sm" tone="orange">
                    Standard Packs need a dedicated playing-card picker.
                  </JimboText>
                  <JimboText size="xs" tone="grey" style={{ display: "block", marginTop: 6 }}>
                    Arcana, Celestial, Spectral, and Buffoon pack flows are wired. Standard Pack support can come next without faking the JAML shape.
                  </JimboText>
                </div>
                <JimboButton tone="orange" size="sm" fullWidth onClick={() => {
                  setActivePackSelection(null);
                  setPickerFlow("pack");
                }}>
                  Back to Packs
                </JimboButton>
              </div>
            ) : (
              <CategoryPicker
                config={CATEGORY_CONFIG_MAP[pickerFlow as SlotCategory]}
                onSelect={handleItemSelect}
              />
            )}
          </div>
        )}
      </JimboModal>

      <JimboModal
        open={activePackDetail !== null && !!activePackDetailSelection?.packName}
        onClose={handleOverlayClose}
        title={activePackDetailSelection?.packName ?? "Pack"}
        className="j-picker-modal"
      >
        {activePackDetail !== null && activePackDetailSelection?.packName && (
          <div style={{ display: "flex", flexDirection: "column", gap: 10, maxWidth: 360 }}>
            <div className="j-inner-panel" style={{ padding: "10px 12px", background: withAlpha(C.DARKEST, 0.84) }}>
              <div style={{ display: "flex", alignItems: "center", gap: 10 }}>
                <JimboSprite name={activePackDetailSelection.packName} sheet="Boosters" width={56} />
                <div style={{ display: "flex", flexDirection: "column", gap: 4 }}>
                  <JimboText size="sm" tone="white">Ante {activePackDetail.ante} pack {((activePackDetailSelection.boosterPacks?.[0] ?? 0) + 1)}</JimboText>
                  <JimboText size="xs" tone="grey">{getPackHelperText(activePackDetailSelection.packName)}</JimboText>
                </div>
              </div>
            </div>

            <div className="j-inner-panel" style={{ padding: "10px 12px" }}>
              <JimboText size="xs" tone="grey" style={{ display: "block", marginBottom: 8 }}>
                Peek
              </JimboText>
              <div style={{ display: "flex", alignItems: "center", gap: 10, marginBottom: 10 }}>
                <JimboSprite name={activePackDetailSelection.packName} sheet="Boosters" width={44} />
                <JimboText size="sm" tone="grey">→</JimboText>
                <JimboSprite name={activePackDetailSelection.value} sheet={categoryToPreviewSheet(activePackDetailSelection.category)} width={48} />
                <div style={{ display: "flex", flexDirection: "column", gap: 3 }}>
                  <JimboText size="sm" tone="white">{activePackDetailSelection.value}</JimboText>
                  <JimboText size="xs" tone="grey">{getSelectionCategoryLabel(activePackDetailSelection.category)} from {activePackDetailSelection.packName}</JimboText>
                </div>
              </div>

              <div className="j-flex j-gap-sm" style={{ flexWrap: "wrap" }}>
                <div className="j-inner-panel" style={{ padding: "6px 8px", minWidth: 96 }}>
                  <JimboText size="micro" tone="grey" style={{ display: "block", marginBottom: 2 }}>Zone</JimboText>
                  <JimboText size="xs" tone={getZoneTextTone(activePackDetailSelection.zone)}>{ZONE_LABEL[activePackDetailSelection.zone]}</JimboText>
                </div>
                <div className="j-inner-panel" style={{ padding: "6px 8px", minWidth: 120 }}>
                  <JimboText size="micro" tone="grey" style={{ display: "block", marginBottom: 2 }}>Clause</JimboText>
                  <JimboText size="xs" tone="white">{activePackDetailSelection.clauseKey}</JimboText>
                </div>
                <div className="j-inner-panel" style={{ padding: "6px 8px", minWidth: 96 }}>
                  <JimboText size="micro" tone="grey" style={{ display: "block", marginBottom: 2 }}>Source</JimboText>
                  <JimboText size="xs" tone="white">boosterPacks: [{activePackDetailSelection.boosterPacks?.join(", ") ?? ""}]</JimboText>
                </div>
              </div>
            </div>

            <div className="j-flex j-gap-sm" style={{ flexWrap: "wrap" }}>
              <JimboButton tone="blue" size="sm" fullWidth onClick={handlePackChange}>
                Re-pick Contents
              </JimboButton>
              <JimboButton tone="red" size="sm" fullWidth onClick={() => handleSlotClear(activePackDetail.ante, activePackDetail.id)}>
                Clear This Pack
              </JimboButton>
            </div>
          </div>
        )}
      </JimboModal>
    </div>
  );
}

// ─── Category Selection Menu ─────────────────────────────────────────────────

export function CategoryMenu({
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

function getPackSlotIndex(slotId: string): number | null {
  const match = /_pack_(\d+)$/.exec(slotId);
  if (!match) return null;
  return Number(match[1]) - 1;
}

function getPackRows(ante: number): number[][] {
  const packCount = ante < 2 ? 4 : 6;
  const rows: number[][] = [];
  for (let i = 1; i <= packCount; i += 2) {
    rows.push([i, i + 1].filter((slot) => slot <= packCount));
  }
  return rows;
}

function getPackFollowupFlow(packName: string): PickerFlow {
  if (packName.includes("Buffoon")) return "joker";
  if (packName.includes("Arcana")) return "tarot";
  if (packName.includes("Celestial")) return "planet";
  if (packName.includes("Spectral")) return "spectral";
  return "packUnsupported";
}

function categoryToPreviewSheet(category: SlotCategory): SpriteSheetType {
  if (category === "joker") return "Jokers";
  if (category === "voucher") return "Vouchers";
  if (category === "tag") return "tags";
  if (category === "boss") return "BlindChips";
  if (category === "pack") return "Boosters";
  return "Tarots";
}

function getPackHelperText(packName: string): string {
  if (packName.includes("Buffoon")) return "Peek the joker this pack is meant to carry.";
  if (packName.includes("Arcana")) return "Peek the tarot card currently attached to this pack.";
  if (packName.includes("Celestial")) return "Peek the planet card currently attached to this pack.";
  if (packName.includes("Spectral")) return "Peek the spectral card currently attached to this pack.";
  return "Peek the item currently attached to this pack.";
}

function getSelectionCategoryLabel(category: SlotCategory): string {
  if (category === "joker") return "Joker";
  if (category === "voucher") return "Voucher";
  if (category === "tag") return "Tag";
  if (category === "boss") return "Boss Blind";
  if (category === "tarot") return "Tarot Card";
  if (category === "planet") return "Planet Card";
  if (category === "spectral") return "Spectral Card";
  return "Pack";
}

function getZoneTextTone(zone: JamlZone): "blue" | "green" | "red" {
  if (zone === "must") return "blue";
  if (zone === "should") return "green";
  return "red";
}

function buildJamlText(antes: Record<number, AnteSelections>): string {
  const byZone: Record<JamlZone, Record<string, { value: string; antes: number[]; boosterPacks?: number[] }[]>> = {
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

      const existing = byZone[zone][key].find(
        item =>
          item.value === sel.value &&
          (item.boosterPacks ?? []).join(",") === (sel.boosterPacks ?? []).join(",")
      );
      if (existing) {
        if (!existing.antes.includes(anteNum)) existing.antes.push(anteNum);
      } else {
        byZone[zone][key].push({ value: sel.value, antes: [anteNum], boosterPacks: sel.boosterPacks });
      }
    }
  }

  const lines: string[] = [];
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
          lines.push(`    antes: [${item.antes.sort((a, b) => a - b).join(", ")}]`);
        }
        if (item.boosterPacks && item.boosterPacks.length > 0) {
          lines.push(`    sources:`);
          lines.push(`      boosterPacks: [${item.boosterPacks.join(", ")}]`);
        }
      }
    }
  }

  return lines.join("\n") + "\n";
}
