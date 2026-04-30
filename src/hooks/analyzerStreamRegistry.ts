"use client";

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
  let cursor = 0;
  const idBase = `${ante}-${key}`;

  function nameFromValue(value: number): string {
    return motelyItemDisplayNameFromValue(value);
  }

  function streamHandle<S>(
    createStream: () => S,
    getNextValue: (stream: S) => number,
  ): StreamHandle {
    let stream: S | null = null;
    return {
      initStream: () => {
        stream = createStream();
        cursor = 0;
      },
      nextItem: () => {
        if (stream === null) throw new Error(`Analyzer stream ${key} was read before initialization.`);
        const value = getNextValue(stream);
        const id = `${idBase}-${cursor++}`;
        return { id, name: nameFromValue(value), value };
      },
    };
  }

  function joker(
    createStream: () => ReturnType<typeof ctx.createShopJokerStream>,
    getNext: (stream: ReturnType<typeof ctx.createShopJokerStream>) => ReturnType<typeof ctx.getNextShopJoker>,
  ): StreamHandle {
    return streamHandle(createStream, (stream) => getNext(stream).item.value);
  }

  function fixedRarityJoker(
    createStream: () => ReturnType<typeof ctx.createLegendaryJokerStream>,
    getNext: (stream: ReturnType<typeof ctx.createLegendaryJokerStream>) => ReturnType<typeof ctx.getNextLegendaryJoker>,
  ): StreamHandle {
    return streamHandle(createStream, (stream) => getNext(stream).item.value);
  }

  function tarot(
    createStream: () => ReturnType<typeof ctx.createShopTarotStream>,
    getNext: (stream: ReturnType<typeof ctx.createShopTarotStream>) => ReturnType<typeof ctx.getNextShopTarot>,
  ): StreamHandle {
    return streamHandle(createStream, (stream) => getNext(stream).item.value);
  }

  function planet(
    createStream: () => ReturnType<typeof ctx.createShopPlanetStream>,
    getNext: (stream: ReturnType<typeof ctx.createShopPlanetStream>) => ReturnType<typeof ctx.getNextShopPlanet>,
  ): StreamHandle {
    return streamHandle(createStream, (stream) => getNext(stream).item.value);
  }

  function spectral(
    createStream: () => ReturnType<typeof ctx.createShopSpectralStream>,
    getNext: (stream: ReturnType<typeof ctx.createShopSpectralStream>) => ReturnType<typeof ctx.getNextShopSpectral>,
  ): StreamHandle {
    return streamHandle(createStream, (stream) => getNext(stream).item.value);
  }

  switch (key) {
    case "shop":
      return streamHandle(
        () =>
          ctx.createShopItemStream(
            ante,
            runState,
            Motely.MotelyShopStreamFlags.Default,
            Motely.MotelyJokerStreamFlags.Default,
          ),
        (stream) => ctx.getNextShopItem(stream).item.value,
      );
    case "shopJoker":
      return joker(
        () => ctx.createShopJokerStream(ante, Motely.MotelyJokerStreamFlags.Default),
        (stream) => ctx.getNextShopJoker(stream),
      );
    case "soulJoker":
      return fixedRarityJoker(
        () => ctx.createLegendaryJokerStream(ante, Motely.MotelyJokerFixedRarityStreamFlags.Default),
        (stream) => ctx.getNextLegendaryJoker(stream),
      );
    case "rareTagJoker":
      return fixedRarityJoker(
        () => ctx.createRareTagJokerStream(ante, Motely.MotelyJokerFixedRarityStreamFlags.Default),
        (stream) => ctx.getNextRareTagJoker(stream),
      );
    case "uncommonTagJoker":
      return fixedRarityJoker(
        () => ctx.createUncommonTagJokerStream(ante, Motely.MotelyJokerFixedRarityStreamFlags.Default),
        (stream) => ctx.getNextUncommonTagJoker(stream),
      );
    case "riffRaffJoker":
      return fixedRarityJoker(
        () => ctx.createRiffRaffJokerStream(ante, Motely.MotelyJokerFixedRarityStreamFlags.Default),
        (stream) => ctx.getNextRiffRaffJoker(stream),
      );
    case "buffoonJoker":
      return joker(
        () => ctx.createBuffoonPackJokerStream(ante, Motely.MotelyJokerStreamFlags.Default),
        (stream) => ctx.getNextBuffoonPackJoker(stream),
      );
    case "judgementJoker":
      return joker(
        () => ctx.createJudgementJokerStream(ante, Motely.MotelyJokerStreamFlags.Default),
        (stream) => ctx.getNextJudgementJoker(stream),
      );
    case "wraithJoker":
      return joker(
        () => ctx.createWraithJokerStream(ante, Motely.MotelyJokerStreamFlags.Default),
        (stream) => ctx.getNextWraithJoker(stream),
      );
    case "shopTarot":
      return tarot(
        () => ctx.createShopTarotStream(ante),
        (stream) => ctx.getNextShopTarot(stream),
      );
    case "shopPlanet":
      return planet(
        () => ctx.createShopPlanetStream(ante),
        (stream) => ctx.getNextShopPlanet(stream),
      );
    case "shopSpectral":
      return spectral(
        () => ctx.createShopSpectralStream(ante),
        (stream) => ctx.getNextShopSpectral(stream),
      );
    case "purpleSealTarot":
      return tarot(
        () => ctx.createPurpleSealTarotStream(ante),
        (stream) => ctx.getNextPurpleSealTarot(stream),
      );
    default:
      return null;
  }
}
