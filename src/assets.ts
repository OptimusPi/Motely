import deckUrl from "../assets/8BitDeck.png";
import blindsUrl from "../assets/BlindChips.png";
import boostersUrl from "../assets/Boosters.png";
import editionsUrl from "../assets/Editions.png";
import enhancersUrl from "../assets/Enhancers.png";
import jokersUrl from "../assets/Jokers.png";
import tarotsUrl from "../assets/Tarots.png";
import vouchersUrl from "../assets/Vouchers.png";
import stickersUrl from "../assets/stickers.png";
import tagsUrl from "../assets/tags.png";
import stakesUrl from "../assets/balatro-stake-chips.png";
import fontUrl from "../assets/fonts/m6x11plus.ttf";

export const JAML_ASSET_FILES = {
  deck: "8BitDeck.png",
  blinds: "BlindChips.png",
  boosters: "Boosters.png",
  editions: "Editions.png",
  enhancers: "Enhancers.png",
  jokers: "Jokers.png",
  tarots: "Tarots.png",
  vouchers: "Vouchers.png",
  stickers: "stickers.png",
  tags: "tags.png",
  stakes: "balatro-stake-chips.png",
  font: "fonts/m6x11plus.ttf",
} as const;

export type JamlAssetKey = keyof typeof JAML_ASSET_FILES;
export type JamlAssetFile = (typeof JAML_ASSET_FILES)[JamlAssetKey];

const ASSET_URLS: Record<JamlAssetKey, string> = {
  deck: deckUrl,
  blinds: blindsUrl,
  boosters: boostersUrl,
  editions: editionsUrl,
  enhancers: enhancersUrl,
  jokers: jokersUrl,
  tarots: tarotsUrl,
  vouchers: vouchersUrl,
  stickers: stickersUrl,
  tags: tagsUrl,
  stakes: stakesUrl,
  font: fontUrl,
};

export function resolveJamlAssetUrl(asset: JamlAssetKey): string {
  const url = ASSET_URLS[asset];
  if (!url) {
    throw new Error(`Unknown Jaml asset '${asset}'.`);
  }
  return url;
}
