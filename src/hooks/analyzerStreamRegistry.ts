"use client";

/* eslint-disable @typescript-eslint/no-explicit-any */

import { motelyItemDisplayNameFromValue } from "../motelyDisplay.js";
import type { StreamItem } from "./useShopStream.js";
import type { AnalyzerLive } from "./useAnalyzer.js";

/**
 * Registry of motely-wasm streams the fullscreen analyzer can surface as
 * lanes. Each entry knows how to (a) open a stream against a live ctx +
 * runState for a given ante, and (b) pull the next item.
 *
 * Streams that produce raw item *values* (jokers, tarots, planets,
 * spectrals) are normalized through `motelyItemDisplayNameFromValue` so
 * downstream rendering uses one shared resolver. Pack-contents streams
 * return arrays in a single call; we flatten to per-card items keyed by
 * pack-pull index so React keys stay stable across reloads.
 */

export type AnalyzerStreamKey =
  | "shop"
  | "soulJoker"
  | "rareTagJoker"
  | "uncommonTagJoker"
  | "riffRaffJoker"
  | "buffoonJoker"
  | "judgementJoker"
  | "wraithJoker"
  | "shopJoker"
  | "shopTarot"
  | "shopPlanet"
  | "shopSpectral"
  | "purpleSealTarot";

export interface AnalyzerStreamMeta {
  key: AnalyzerStreamKey;
  label: string;
  /** Tone hint for the lane header. */
  tone: "gold" | "purple" | "blue" | "spectral" | "default";
  /** Whether this stream is on by default in the picker. */
  defaultEnabled: boolean;
}

export const ANALYZER_STREAM_META: Record<AnalyzerStreamKey, AnalyzerStreamMeta> = {
  shop: { key: "shop", label: "Shop", tone: "default", defaultEnabled: true },
  shopJoker: { key: "shopJoker", label: "Shop Jokers", tone: "default", defaultEnabled: false },
  soulJoker: { key: "soulJoker", label: "Soul (Legendary)", tone: "gold", defaultEnabled: true },
  rareTagJoker: { key: "rareTagJoker", label: "Rare Tag Jokers", tone: "default", defaultEnabled: false },
  uncommonTagJoker: { key: "uncommonTagJoker", label: "Uncommon Tag Jokers", tone: "default", defaultEnabled: false },
  riffRaffJoker: { key: "riffRaffJoker", label: "Riff Raff", tone: "default", defaultEnabled: false },
  buffoonJoker: { key: "buffoonJoker", label: "Buffoon Pack Jokers", tone: "default", defaultEnabled: false },
  judgementJoker: { key: "judgementJoker", label: "Judgement Jokers", tone: "purple", defaultEnabled: false },
  wraithJoker: { key: "wraithJoker", label: "Wraith Jokers", tone: "spectral", defaultEnabled: false },
  shopTarot: { key: "shopTarot", label: "Shop Tarots", tone: "purple", defaultEnabled: false },
  shopPlanet: { key: "shopPlanet", label: "Shop Planets", tone: "blue", defaultEnabled: false },
  shopSpectral: { key: "shopSpectral", label: "Shop Spectrals", tone: "spectral", defaultEnabled: false },
  purpleSealTarot: { key: "purpleSealTarot", label: "Purple Seal Tarots", tone: "purple", defaultEnabled: false },
};

export const DEFAULT_ENABLED_STREAMS: AnalyzerStreamKey[] = (
  Object.values(ANALYZER_STREAM_META)
    .filter((m) => m.defaultEnabled)
    .map((m) => m.key)
);

interface StreamHandle {
  initStream: () => void;
  nextItem: () => StreamItem;
}

/**
 * Build init/next callbacks for a (key, ante) pair against the live ctx.
 * The component wraps these with useMotelyStream. Items are normalized to
 * { id, name, value } so the same ShopItem renderer can show every lane.
 */
export function buildStreamHandle(
  live: AnalyzerLive,
  ante: number,
  key: AnalyzerStreamKey,
): StreamHandle | null {
  const ctx = live.ctx;
  const Motely = live.Motely;
  const runState = live.runStates[ante];
  let stream: any = null;
  let cursor = 0;
  const idBase = `${ante}-${key}`;

  function nameFromValue(value: number): string {
    return motelyItemDisplayNameFromValue(value);
  }

  function joker(streamFactoryName: string, nextName: string): StreamHandle {
    return {
      initStream: () => {
        stream = ctx[streamFactoryName](ante, Motely.MotelyJokerStreamFlags.Default);
        cursor = 0;
      },
      nextItem: () => {
        const r = ctx[nextName](stream);
        const value = r.joker?.value ?? r.item?.value ?? 0;
        const id = `${idBase}-${cursor++}`;
        return { id, name: nameFromValue(value), value };
      },
    };
  }

  function fixedRarityJoker(streamFactoryName: string, nextName: string): StreamHandle {
    return {
      initStream: () => {
        stream = ctx[streamFactoryName](ante, Motely.MotelyJokerFixedRarityStreamFlags.Default);
        cursor = 0;
      },
      nextItem: () => {
        const r = ctx[nextName](stream);
        const value = r.joker?.value ?? 0;
        const id = `${idBase}-${cursor++}`;
        return { id, name: nameFromValue(value), value };
      },
    };
  }

  function tarot(streamFactoryName: string, nextName: string): StreamHandle {
    return {
      initStream: () => {
        stream = ctx[streamFactoryName](ante);
        cursor = 0;
      },
      nextItem: () => {
        const r = ctx[nextName](stream);
        const value = r.tarot?.value ?? r.item?.value ?? 0;
        const id = `${idBase}-${cursor++}`;
        return { id, name: nameFromValue(value), value };
      },
    };
  }

  function planet(streamFactoryName: string, nextName: string): StreamHandle {
    return {
      initStream: () => {
        stream = ctx[streamFactoryName](ante);
        cursor = 0;
      },
      nextItem: () => {
        const r = ctx[nextName](stream);
        const value = r.planet?.value ?? 0;
        const id = `${idBase}-${cursor++}`;
        return { id, name: nameFromValue(value), value };
      },
    };
  }

  function spectral(streamFactoryName: string, nextName: string): StreamHandle {
    return {
      initStream: () => {
        stream = ctx[streamFactoryName](ante);
        cursor = 0;
      },
      nextItem: () => {
        const r = ctx[nextName](stream);
        const value = r.spectral?.value ?? 0;
        const id = `${idBase}-${cursor++}`;
        return { id, name: nameFromValue(value), value };
      },
    };
  }

  switch (key) {
    case "shop":
      return {
        initStream: () => {
          stream = ctx.createShopItemStream(
            ante,
            runState,
            Motely.MotelyShopStreamFlags.Default,
            Motely.MotelyJokerStreamFlags.Default,
          );
          cursor = 0;
        },
        nextItem: () => {
          const r = ctx.getNextShopItem(stream);
          const value = r.item.value;
          const id = `${idBase}-${cursor++}`;
          return { id, name: nameFromValue(value), value };
        },
      };
    case "shopJoker":
      return joker("createShopJokerStream", "getNextShopJoker");
    case "soulJoker":
      return fixedRarityJoker("createSoulJokerStream", "getNextSoulJoker");
    case "rareTagJoker":
      return fixedRarityJoker("createRareTagJokerStream", "getNextRareTagJoker");
    case "uncommonTagJoker":
      return fixedRarityJoker("createUncommonTagJokerStream", "getNextUncommonTagJoker");
    case "riffRaffJoker":
      return fixedRarityJoker("createRiffRaffJokerStream", "getNextRiffRaffJoker");
    case "buffoonJoker":
      return joker("createBuffoonPackJokerStream", "getNextBuffoonPackJoker");
    case "judgementJoker":
      return joker("createJudgementJokerStream", "getNextJudgementJoker");
    case "wraithJoker":
      return joker("createWraithJokerStream", "getNextWraithJoker");
    case "shopTarot":
      return tarot("createShopTarotStream", "getNextShopTarot");
    case "shopPlanet":
      return planet("createShopPlanetStream", "getNextShopPlanet");
    case "shopSpectral":
      return spectral("createShopSpectralStream", "getNextShopSpectral");
    case "purpleSealTarot":
      return tarot("createPurpleSealTarotStream", "getNextPurpleSealTarot");
    default:
      return null;
  }
}
