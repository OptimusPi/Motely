"use client";

import { useState, useCallback } from "react";
import { loadMotelyWasm } from "./loadMotelyWasm.js";
import { extractVisualJamlItems } from "../utils/jamlMapPreview.js";
import { motelyItemDisplayNameFromValue } from "../motelyDisplay.js";
import type { AnalyzerAnteView, AnalyzerItem } from "../components/AnalyzerExplorer.js";

export type AnalyzerStatus = "idle" | "running" | "done" | "error";

export function useAnalyzer(motelyWasmUrl: string) {
  const [antes, setAntes] = useState<AnalyzerAnteView[]>([]);
  const [status, setStatus] = useState<AnalyzerStatus>("idle");
  const [error, setError] = useState<string | null>(null);

  const analyze = useCallback(async (seed: string, deck: string, stake: string, jaml?: string) => {
    setAntes([]);
    setStatus("running");
    setError(null);

    try {
      const { MotelyWasm, Motely } = await loadMotelyWasm(motelyWasmUrl);

      const deckEnum = Motely.MotelyDeck[deck as keyof typeof Motely.MotelyDeck] ?? Motely.MotelyDeck.Red;
      const stakeEnum = Motely.MotelyStake[stake as keyof typeof Motely.MotelyStake] ?? Motely.MotelyStake.White;

      const desiredNames = new Set<string>();
      if (jaml) {
        const groups = extractVisualJamlItems(jaml);
        for (const item of [...groups.must, ...groups.should]) {
          desiredNames.add(item.value.toLowerCase());
        }
      }

      const ctx = MotelyWasm.createSearchContext(seed, deckEnum, stakeEnum);
      const bossStream = ctx.createBossStream();
      let runState = { voucherBitfield: 0, bossBitfield: 0 };
      const results: AnalyzerAnteView[] = [];

      for (let ante = 1; ante <= 8; ante++) {
        const bossResult = ctx.getNextBossForAnte(bossStream, ante, runState);
        const bossName = Motely.MotelyBossBlind[bossResult.boss] ?? `Unknown(${bossResult.boss})`;
        runState = bossResult.runState;

        const voucherResult = ctx.getAnteFirstVoucher(ante, runState);
        const voucherName = Motely.MotelyVoucher[voucherResult.voucher] ?? `Unknown(${voucherResult.voucher})`;
        runState = voucherResult.runState;

        const tagStream = ctx.createTagStream(ante);
        const tag1 = ctx.getNextTag(tagStream);
        const tag2 = ctx.getNextTag(tagStream);

        const packStream = ctx.createBoosterPackStream(ante);
        const packs: string[] = [];
        for (let p = 0; p < 2; p++) {
          const packResult = ctx.getNextBoosterPack(packStream);
          packs.push(Motely.MotelyBoosterPack[packResult.pack] ?? `Unknown(${packResult.pack})`);
        }

        const shopStream = ctx.createShopItemStream(
          ante,
          runState,
          Motely.MotelyShopStreamFlags.Default,
          Motely.MotelyJokerStreamFlags.Default,
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
          smallBlindTag: Motely.MotelyTag[tag1.tag] ?? `Unknown(${tag1.tag})`,
          bigBlindTag: Motely.MotelyTag[tag2.tag] ?? `Unknown(${tag2.tag})`,
          packs,
          shop,
        });
      }

      setAntes(results);
      setStatus("done");
    } catch (e) {
      setError(e instanceof Error ? e.message : String(e));
      setStatus("error");
    }
  }, [motelyWasmUrl]);

  const clearError = useCallback(() => {
    setError(null);
    setStatus((s) => (s === "error" ? "idle" : s));
  }, []);

  return { antes, status, error, analyze, clearError };
}
