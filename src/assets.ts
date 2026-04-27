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
} as const;

export type JamlAssetKey = keyof typeof JAML_ASSET_FILES;
export type JamlAssetFile = (typeof JAML_ASSET_FILES)[JamlAssetKey];

const assetKeyByFileName = Object.fromEntries(
  Object.entries(JAML_ASSET_FILES).map(([key, fileName]) => [fileName, key]),
) as Record<JamlAssetFile, JamlAssetKey>;

const defaultAssetUrls: Record<JamlAssetKey, string> = {
  deck: new URL(`../assets/${JAML_ASSET_FILES.deck}`, import.meta.url).href,
  blinds: new URL(`../assets/${JAML_ASSET_FILES.blinds}`, import.meta.url).href,
  boosters: new URL(`../assets/${JAML_ASSET_FILES.boosters}`, import.meta.url).href,
  editions: new URL(`../assets/${JAML_ASSET_FILES.editions}`, import.meta.url).href,
  enhancers: new URL(`../assets/${JAML_ASSET_FILES.enhancers}`, import.meta.url).href,
  jokers: new URL(`../assets/${JAML_ASSET_FILES.jokers}`, import.meta.url).href,
  tarots: new URL(`../assets/${JAML_ASSET_FILES.tarots}`, import.meta.url).href,
  vouchers: new URL(`../assets/${JAML_ASSET_FILES.vouchers}`, import.meta.url).href,
  stickers: new URL(`../assets/${JAML_ASSET_FILES.stickers}`, import.meta.url).href,
  tags: new URL(`../assets/${JAML_ASSET_FILES.tags}`, import.meta.url).href,
  stakes: new URL(`../assets/${JAML_ASSET_FILES.stakes}`, import.meta.url).href,
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
