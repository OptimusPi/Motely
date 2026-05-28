"use client";

import React, { useEffect, useMemo, useState } from "react";
import {
  Motely,
  type MotelyJamlyzerResult,
  type MotelyJamlyzerSeedResult,
  MotelyBossBlind,
  MotelyVoucher,
  MotelyTag,
  MotelyBoosterPack
} from "motely-wasm";
import { ensureMotelyReady } from "../lib/motely/runtime.js";
import { JimboInnerPanel, JimboPanel } from "../ui/panel.js";
import { JimboText } from "../ui/jimboText.js";
import { JamlSeedSpinner } from "./JamlSeedSpinner.js";
import { JimboSpinner } from "../ui/JimboSpinner.js";
import { BOSSES, VOUCHERS, TAGS, BOOSTER_PACKS } from "../sprites/spriteData.js";
import { MOTELY_ITEM_FORMATS_BY_VALUE } from "../decode/motelyItemFormats.js";
import {
  JamlBoss,
  JamlVoucher,
  JamlTag,
  JamlGameCard,
  resolveAnalyzerShopItem,
  type AnalyzerResolvedItem
} from "./GameCard.js";

export interface JamlyzerProps {
  /** Full JAML document. Seeds come from the top-level `seeds:` array via Motely. */
  jaml: string;
  className?: string;
  style?: React.CSSProperties;
}

type JamlyzerLoadState =
  | { status: "loading" }
  | { status: "ready"; result: MotelyJamlyzerResult; elapsedMs: number }
  | { status: "error"; message: string };

function seedMatches(row: MotelyJamlyzerSeedResult): boolean {
  return (row.score ?? 0) >= 1;
}

function getBossDisplayName(bossVal: MotelyBossBlind): string {
  const key = MotelyBossBlind[bossVal];
  if (!key) return "Small Blind";
  const normalizedKey = key.toLowerCase();
  const found = BOSSES.find(b => b.name.replace(/[^a-zA-Z0-9]/g, "").toLowerCase() === normalizedKey);
  return found ? found.name : key;
}

function getVoucherDisplayName(voucherVal: MotelyVoucher): string {
  const key = MotelyVoucher[voucherVal];
  if (!key) return "";
  const normalizedKey = key.toLowerCase();
  const found = VOUCHERS.find(v => v.name.replace(/[^a-zA-Z0-9]/g, "").toLowerCase() === normalizedKey);
  return found ? found.name : key;
}

function getTagDisplayName(tagVal: MotelyTag): string {
  const key = MotelyTag[tagVal];
  if (!key) return "";
  const normalizedKey = key.toLowerCase();
  const found = TAGS.find(t => t.name.replace(/[^a-zA-Z0-9]/g, "").toLowerCase() === normalizedKey);
  return found ? found.name : key;
}

function getBoosterPackDisplayName(packVal: MotelyBoosterPack): string {
  const key = MotelyBoosterPack[packVal];
  if (!key) return "";
  const normalizedKey = (key + "pack").toLowerCase();
  const found = BOOSTER_PACKS.find(b => b.name.replace(/[^a-zA-Z0-9]/g, "").toLowerCase() === normalizedKey);
  return found ? found.name : key + " Pack";
}

function getResolvedItem(value: number, scale = 0.5): AnalyzerResolvedItem {
  const format = MOTELY_ITEM_FORMATS_BY_VALUE[value as keyof typeof MOTELY_ITEM_FORMATS_BY_VALUE];
  if (format) {
    return resolveAnalyzerShopItem({
      id: String(value),
      name: format.displayName,
      value: value
    }, scale);
  }
  return { kind: "unknown", label: `Unknown #${value}` };
}

export function Jamlyzer({ jaml, className = "", style }: JamlyzerProps) {
  const [load, setLoad] = useState<JamlyzerLoadState>({ status: "loading" });
  const [index, setIndex] = useState(0);
  const [selectedAnte, setSelectedAnte] = useState(1);
  const [lastJaml, setLastJaml] = useState(jaml);

  // Reset to loading the moment `jaml` changes — render-phase derivation
  // (React's "Adjusting state when a prop changes" pattern) avoids the
  // cascading render that synchronous setState-in-effect would cause.
  if (jaml !== lastJaml) {
    setLastJaml(jaml);
    setLoad({ status: "loading" });
    setIndex(0);
    setSelectedAnte(1);
  }

  useEffect(() => {
    let cancelled = false;

    void (async () => {
      try {
        await ensureMotelyReady();
        const trimmed = jaml.trim();
        if (!trimmed) {
          throw new Error("Write a JAML filter first.");
        }
        const validation = Motely.validateJaml(trimmed);
        if (validation !== "valid") {
          throw new Error(String(validation ?? "Invalid JAML"));
        }
        const t0 = performance.now();
        const result = Motely.analyzeJamlSeeds(trimmed, []);
        const elapsedMs = performance.now() - t0;
        if (cancelled) return;
        if (result.error) {
          throw new Error(result.error);
        }
        setLoad({ status: "ready", result, elapsedMs });
      } catch (error) {
        if (cancelled) return;
        setLoad({
          status: "error",
          message: error instanceof Error ? error.message : String(error),
        });
      }
    })();

    return () => {
      cancelled = true;
    };
  }, [jaml]);

  const rows = useMemo(
    () => (load.status === "ready" ? load.result.seeds : []),
    [load],
  );
  const seedList = useMemo(() => rows.map((row) => row.seed), [rows]);
  const safeIndex = seedList.length > 0 ? Math.min(index, seedList.length - 1) : 0;
  const current = rows[safeIndex];
  const activeSeed = current?.seed ?? "";

  const handleSeedChange = (seed: string) => {
    const nextIndex = seedList.indexOf(seed);
    if (nextIndex >= 0) setIndex(nextIndex);
  };

  const rootClass = ["j-jamlyzer", className].filter(Boolean).join(" ");

  if (load.status === "loading") {
    return (
      <div className={rootClass} style={style}>
        <JimboPanel className="j-jamlyzer__panel j-jamlyzer__panel--hint">
          <JimboText size="sm" tone="white" className="j-text-center">
            Analyzing seeds…
          </JimboText>
        </JimboPanel>
      </div>
    );
  }

  if (load.status === "error") {
    return (
      <div className={rootClass} style={style}>
        <JimboPanel className="j-jamlyzer__panel j-jamlyzer__panel--hint">
          <JimboText size="xs" tone="red" className="j-text-center">
            {load.message}
          </JimboText>
        </JimboPanel>
      </div>
    );
  }

  const { elapsedMs, result } = load;
  const matchCount = rows.filter(seedMatches).length;
  const isMatch = current ? seedMatches(current) : false;
  const tallyLine =
    result.tallyLabels && result.tallyLabels.length > 0 && current
      ? result.tallyLabels.map((label, i) => `${label}: ${current.tallies[i] ?? 0}`).join(" · ")
      : null;

  const hasAnalysis = !!current?.analysis;

  return (
    <div className={rootClass} style={style}>
      <JimboText size="xs" tone="white" className="j-jamlyzer__stats j-text-center">
        {elapsedMs.toFixed(0)} ms · {rows.length} seeds · {matchCount} match
      </JimboText>

      <div className="j-jamlyzer__spinner">
        <JamlSeedSpinner
          seeds={seedList}
          value={activeSeed}
          onChange={handleSeedChange}
          label=""
          placeholder="Add seeds: to JAML"
          variant="dark"
          aria-label="Jamlyzer seed"
        />
      </div>

      {current ? (
        <>
          <JimboPanel
            className={[
              "j-jamlyzer__panel",
              "j-jamlyzer__panel--verdict",
              isMatch ? "j-jamlyzer__panel--match j-glow--match" : "j-jamlyzer__panel--miss",
            ].join(" ")}
          >
            <JimboText size="md" tone={isMatch ? "green" : "red"} className="j-text-center">
              {isMatch ? `Match · score ${current.score}` : `No match · score ${current.score}`}
            </JimboText>
          </JimboPanel>

          {tallyLine ? (
            <JimboInnerPanel className="j-jamlyzer__tallies">
              <JimboText size="xs" tone="white" className="j-text-center">
                {tallyLine}
              </JimboText>
            </JimboInnerPanel>
          ) : null}

          {hasAnalysis && current.analysis?.antes && (
            <JimboInnerPanel className="j-jamlyzer__details">
              <JimboSpinner
                value={`Ante ${selectedAnte}`}
                onPrev={() => setSelectedAnte(a => Math.max(1, a - 1))}
                onNext={() => setSelectedAnte(a => Math.min(8, a + 1))}
                canPrev={selectedAnte > 1}
                canNext={selectedAnte < 8}
              />

              {(() => {
                const anteData = current.analysis?.antes.find(a => a.ante === selectedAnte);
                if (!anteData) {
                  return (
                    <JimboText size="xs" tone="grey" className="j-text-center">
                      No analysis for Ante {selectedAnte}
                    </JimboText>
                  );
                }

                return (
                  <div className="j-stack j-stack--gap-sm">
                    {/* Boss & Voucher */}
                    <div className="j-jamlyzer__details-section">
                      <JimboText size="xs" tone="gold" className="j-text-center">
                        Boss & Voucher
                      </JimboText>
                      <div className="j-row j-row--gap-md j-row--justify-center j-row--align-center">
                        <div className="j-stack j-stack--gap-xs j-stack--align-center">
                          <div className={`j-jamlyzer__card-wrap ${anteData.bossMatched ? "j-jamlyzer__card-wrap--matched" : ""}`}>
                            <JamlBoss bossName={getBossDisplayName(anteData.boss)} scale={0.5} />
                          </div>
                          <JimboText size="micro" tone="white" className="j-text-center">
                            {getBossDisplayName(anteData.boss)}
                          </JimboText>
                        </div>
                        <div className="j-stack j-stack--gap-xs j-stack--align-center">
                          <div className={`j-jamlyzer__card-wrap ${anteData.voucherMatched ? "j-jamlyzer__card-wrap--matched" : ""}`}>
                            <JamlVoucher voucherName={getVoucherDisplayName(anteData.voucher)} scale={0.5} />
                          </div>
                          <JimboText size="micro" tone="white" className="j-text-center">
                            {getVoucherDisplayName(anteData.voucher)}
                          </JimboText>
                        </div>
                      </div>
                    </div>

                    {/* Tags */}
                    <div className="j-jamlyzer__details-section">
                      <JimboText size="xs" tone="gold" className="j-text-center">
                        Tags
                      </JimboText>
                      <div className="j-row j-row--gap-md j-row--justify-center j-row--align-center">
                        <div className="j-stack j-stack--gap-xs j-stack--align-center">
                          <div className={`j-jamlyzer__card-wrap ${anteData.smallBlindTagMatched ? "j-jamlyzer__card-wrap--matched" : ""}`}>
                            <JamlTag tagName={getTagDisplayName(anteData.smallBlindTag)} scale={0.5} />
                          </div>
                          <JimboText size="micro" tone="white" className="j-text-center">
                            Small: {getTagDisplayName(anteData.smallBlindTag)}
                          </JimboText>
                        </div>
                        <div className="j-stack j-stack--gap-xs j-stack--align-center">
                          <div className={`j-jamlyzer__card-wrap ${anteData.bigBlindTagMatched ? "j-jamlyzer__card-wrap--matched" : ""}`}>
                            <JamlTag tagName={getTagDisplayName(anteData.bigBlindTag)} scale={0.5} />
                          </div>
                          <JimboText size="micro" tone="white" className="j-text-center">
                            Big: {getTagDisplayName(anteData.bigBlindTag)}
                          </JimboText>
                        </div>
                      </div>
                    </div>

                    {/* Shop Queue */}
                    <div className="j-jamlyzer__details-section">
                      <JimboText size="xs" tone="gold" className="j-text-center">
                        Shop Queue
                      </JimboText>
                      {anteData.shopQueue && anteData.shopQueue.length > 0 ? (
                        <div className="j-jamlyzer__cards-grid">
                          {anteData.shopQueue.map((item, idx) => {
                            const resolved = getResolvedItem(item.item.value, 0.45);
                            const isMatched = item.matched;
                            return (
                              <div
                                key={idx}
                                className={`j-jamlyzer__card-wrap ${isMatched ? "j-jamlyzer__card-wrap--matched" : ""}`}
                              >
                                {resolved.kind === "voucher" && (
                                  <JamlVoucher voucherName={resolved.voucherName} scale={0.45} />
                                )}
                                {(resolved.kind === "joker" || resolved.kind === "consumable" || resolved.kind === "playing") && (
                                  <JamlGameCard card={resolved.card} type={resolved.type} />
                                )}
                                {resolved.kind === "unknown" && (
                                  <div className="j-game-card j-game-card--unknown" style={{ "--j-card-width": `${71 * 0.45}px` } as React.CSSProperties}>
                                    <JimboText size="micro" tone="grey" className="j-text-center">?</JimboText>
                                  </div>
                                )}
                              </div>
                            );
                          })}
                        </div>
                      ) : (
                        <JimboText size="xs" tone="grey" className="j-text-center">
                          Empty
                        </JimboText>
                      )}
                    </div>

                    {/* Packs */}
                    <div className="j-jamlyzer__details-section">
                      <JimboText size="xs" tone="gold" className="j-text-center">
                        Booster Packs
                      </JimboText>
                      {anteData.packs && anteData.packs.length > 0 ? (
                        <div className="j-stack j-stack--gap-sm">
                          {anteData.packs.map((pack, packIdx) => (
                            <div key={packIdx} className="j-stack j-stack--gap-xs">
                              <JimboText size="xs" tone="white" className="j-text-center">
                                {getBoosterPackDisplayName(pack.type)}
                              </JimboText>
                              <div className="j-jamlyzer__cards-grid">
                                {pack.items.map((item, itemIdx) => {
                                  const resolved = getResolvedItem(item.item.value, 0.45);
                                  const isMatched = item.matched;
                                  return (
                                    <div
                                      key={itemIdx}
                                      className={`j-jamlyzer__card-wrap ${isMatched ? "j-jamlyzer__card-wrap--matched" : ""}`}
                                    >
                                      {resolved.kind === "voucher" && (
                                        <JamlVoucher voucherName={resolved.voucherName} scale={0.45} />
                                      )}
                                      {(resolved.kind === "joker" || resolved.kind === "consumable" || resolved.kind === "playing") && (
                                        <JamlGameCard card={resolved.card} type={resolved.type} />
                                      )}
                                      {resolved.kind === "unknown" && (
                                        <div className="j-game-card j-game-card--unknown" style={{ "--j-card-width": `${71 * 0.45}px` } as React.CSSProperties}>
                                          <JimboText size="micro" tone="grey" className="j-text-center">?</JimboText>
                                        </div>
                                      )}
                                    </div>
                                  );
                                })}
                              </div>
                            </div>
                          ))}
                        </div>
                      ) : (
                        <JimboText size="xs" tone="grey" className="j-text-center">
                          No packs opened
                        </JimboText>
                      )}
                    </div>
                  </div>
                );
              })()}
            </JimboInnerPanel>
          )}
        </>
      ) : (
        <JimboPanel className="j-jamlyzer__panel j-jamlyzer__panel--hint">
          <JimboText size="xs" tone="white" className="j-text-center">
            No seeds in JAML. Run Motely CLI with --save-seeds or add a seeds: list.
          </JimboText>
        </JimboPanel>
      )}
    </div>
  );
}

