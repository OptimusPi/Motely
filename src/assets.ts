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
} as const;

export type JamlAssetKey = keyof typeof JAML_ASSET_FILES;
export type JamlAssetFile = (typeof JAML_ASSET_FILES)[JamlAssetKey];

const assetKeyByFileName = Object.fromEntries(
  Object.entries(JAML_ASSET_FILES).map(([key, fileName]) => [fileName, key]),
) as Record<JamlAssetFile, JamlAssetKey>;

import { JAML_UI_VERSION } from "./version.js";

const CDN_BASE = `https://cdn.seedfinder.app/jaml-ui/${JAML_UI_VERSION}/assets/`;

const defaultAssetUrls: Record<JamlAssetKey, string> = {
  deck: `${CDN_BASE}${JAML_ASSET_FILES.deck}`,
  blinds: `${CDN_BASE}${JAML_ASSET_FILES.blinds}`,
  boosters: `${CDN_BASE}${JAML_ASSET_FILES.boosters}`,
  editions: `${CDN_BASE}${JAML_ASSET_FILES.editions}`,
  enhancers: `${CDN_BASE}${JAML_ASSET_FILES.enhancers}`,
  jokers: `${CDN_BASE}${JAML_ASSET_FILES.jokers}`,
  tarots: `${CDN_BASE}${JAML_ASSET_FILES.tarots}`,
  vouchers: `${CDN_BASE}${JAML_ASSET_FILES.vouchers}`,
  stickers: `${CDN_BASE}${JAML_ASSET_FILES.stickers}`,
  tags: `${CDN_BASE}${JAML_ASSET_FILES.tags}`,
};

let customAssetBaseUrl: string | null = null;

function normalizeBaseUrl(baseUrl: string): string {
  const trimmed = baseUrl.trim();
  if (trimmed.length === 0) {
    throw new Error("Jaml asset base URL must not be empty.");
  }
  return trimmed.endsWith("/") ? trimmed : `${trimmed}/`;
}

function joinAssetUrl(baseUrl: string, fileName: JamlAssetFile): string {
  const normalized = normalizeBaseUrl(baseUrl);
  if (/^[a-zA-Z][a-zA-Z\d+\-.]*:/.test(normalized) || normalized.startsWith("//")) {
    return new URL(fileName, normalized).href;
  }
  return `${normalized}${fileName}`;
}

export function setJamlAssetBaseUrl(baseUrl: string | null | undefined): void {
  if (baseUrl == null) {
    customAssetBaseUrl = null;
    return;
  }

  const trimmed = baseUrl.trim();
  customAssetBaseUrl = trimmed.length === 0 ? null : normalizeBaseUrl(trimmed);
}

export function clearJamlAssetBaseUrl(): void {
  customAssetBaseUrl = null;
}

export function resolveJamlAssetUrl(asset: JamlAssetKey | JamlAssetFile): string {
  const assetKey = asset in JAML_ASSET_FILES
    ? (asset as JamlAssetKey)
    : assetKeyByFileName[asset as JamlAssetFile];

  if (!assetKey) {
    throw new Error(`Unknown Jaml asset '${asset}'.`);
  }

  if (customAssetBaseUrl) {
    return joinAssetUrl(customAssetBaseUrl, JAML_ASSET_FILES[assetKey]);
  }

  return defaultAssetUrls[assetKey];
}

export function getDefaultJamlAssetUrlMap(): Readonly<Record<JamlAssetKey, string>> {
  return defaultAssetUrls;
}
