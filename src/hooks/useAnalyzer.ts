"use client";

import { useState, useCallback } from "react";
import { type Motely } from "motely-wasm";
import { extractVisualJamlItems } from "../utils/jamlMapPreview.js";
import { motelyItemDisplayNameFromValue } from "../motelyDisplay.js";
import type { AnalyzerAnteView, AnalyzerItem } from "../components/AnalyzerExplorer.js";

export type AnalyzerStatus = "idle" | "running" | "done" | "error";

export type MotelyJsRunState = { voucherBitfield: number; bossBitfield: number };

export interface AnalyzerLive {
  ctx: ReturnType<typeof Motely.MotelyWasm.createSearchContext>;
  Motely: typeof Motely;
  runStates: Record<number, MotelyJsRunState>;
  desiredNames: ReadonlySet<string>;
  seed: string;
  deck: string;
  stake: string;
}

export function useAnalyzer(motely: typeof Motely | null) {
  const [antes, setAntes] = useState<AnalyzerAnteView[]>([]);
  const [status, setStatus] = useState<AnalyzerStatus>("idle");
  const [error, setError] = useState<string | null>(null);
  const [live, setLive] = useState<AnalyzerLive | null>(null);
  const [tallyColumns, setTallyColumns] = useState<number[][]>([]);
  const [tallyLabels, setTallyLabels] = useState<string[]>([]);

  const analyze = useCallback(async (seed: string, deck: string, stake: string, jaml?: string) => {
    if (!motely) {
      setError("motely-wasm not ready");
      setStatus("error");
      return;
    }
    const MotelyWasm = motely.MotelyWasm;

    setAntes([]);
    setLive(null);
    setTallyColumns([]);
    setTallyLabels([]);
    setStatus("running");
    setError(null);

    try {
      const deckEnum = motely.MotelyDeck[deck as keyof typeof motely.MotelyDeck] ?? motely.MotelyDeck.Red;
      const stakeEnum = motely.MotelyStake[stake as keyof typeof motely.MotelyStake] ?? motely.MotelyStake.White;

      const desiredNames = new Set<string>();
      if (jaml) {
        const groups = extractVisualJamlItems(jaml);
        for (const item of [...groups.must, ...groups.should]) {
          desiredNames.add(item.value.toLowerCase());
        }

        const labels = MotelyWasm.getTallyLabels(jaml);
        setTallyLabels(labels);
      }

      const ctx = MotelyWasm.createSearchContext(seed, deckEnum, stakeEnum);
      const bossStream = ctx.createBossStream();
      let runState: MotelyJsRunState = { voucherBitfield: 0, bossBitfield: 0 };
      const results: AnalyzerAnteView[] = [];
      const runStates: Record<number, MotelyJsRunState> = {};

      for (let ante = 1; ante <= 39; ante++) {
        const bossResult = ctx.getNextBossForAnte(bossStream, ante, runState);
        const bossName = motely.MotelyBossBlind[bossResult.boss] ?? `Unknown(${bossResult.boss})`;
        runState = bossResult.runState;

        const voucherResult = ctx.getAnteFirstVoucher(ante, runState);
        const voucherName = motely.MotelyVoucher[voucherResult.voucher] ?? `Unknown(${voucherResult.voucher})`;
        runState = voucherResult.runState;
        runStates[ante] = { ...runState };

        const tagStream = ctx.createTagStream(ante);
        const tag1 = ctx.getNextTag(tagStream);
        const tag2 = ctx.getNextTag(tagStream);

        const packStream = ctx.createBoosterPackStream(ante);
        const packs: string[] = [];
        for (let p = 0; p < 2; p++) {
          const packResult = ctx.getNextBoosterPack(packStream);
          packs.push(motely.MotelyBoosterPack[packResult.pack] ?? `Unknown(${packResult.pack})`);
        }

        const shopStream = ctx.createShopItemStream(
          ante,
          runState,
          motely.MotelyShopStreamFlags.Default,
          motely.MotelyJokerStreamFlags.Default,
        );
        const shop: AnalyzerItem[] = [];
        for (let i = 0; i < 4; i++) {
          const itemResult = ctx.getNextShopItem(shopStream);
          const name = motelyItemDisplayNameFromValue(itemResult.item.value);
          const desired = desiredNames.size > 0 && desiredNames.has(name.toLowerCase());
          shop.push({ id: `${ante}-shop-${i}`, name, value: itemResult.item.value, desired });
        }

        results.push({
          ante,
          boss: bossName,
          voucher: voucherName,
          smallBlindTag: motely.MotelyTag[tag1.tag] ?? `Unknown(${tag1.tag})`,
          bigBlindTag: motely.MotelyTag[tag2.tag] ?? `Unknown(${tag2.tag})`,
          packs,
          shop,
        });
      }

      setAntes(results);
      setLive({ ctx, Motely: motely, runStates, desiredNames, seed, deck, stake });
      setStatus("done");
    } catch (e) {
      setError(e instanceof Error ? e.message : String(e));
      setStatus("error");
    }
  }, [motely]);

  const clearError = useCallback(() => {
    setError(null);
    setStatus((s) => (s === "error" ? "idle" : s));
  }, []);

  return { antes, status, error, analyze, clearError, live, tallyColumns, tallyLabels };
}
