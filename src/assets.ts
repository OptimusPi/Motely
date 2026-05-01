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
  font: "fonts/m6x11plusplus.otf",
} as const;

export type JamlAssetKey = keyof typeof JAML_ASSET_FILES;
export type JamlAssetFile = (typeof JAML_ASSET_FILES)[JamlAssetKey];

const assetKeyByFileName = Object.fromEntries(
  Object.entries(JAML_ASSET_FILES).map(([key, fileName]) => [fileName, key]),
) as Record<JamlAssetFile, JamlAssetKey>;

const defaultAssetUrls = Object.fromEntries(
  (Object.entries(JAML_ASSET_FILES) as Array<[JamlAssetKey, JamlAssetFile]>).map(
    ([key, fileName]) => [key, new URL(`../assets/${fileName}`, import.meta.url).href],
  ),
) as Record<JamlAssetKey, string>;

let customAssetBaseUrl: string | null = null;

function normalizeBaseUrl(baseUrl: string): string {
  return baseUrl.endsWith("/") ? baseUrl : `${baseUrl}/`;
}

// `new URL(file, base)` requires `base` to be absolute. weejoker.app passes
// "/assets" (relative path), so we fall back to string concatenation for that case.
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

export function resolveJamlAssetUrl(asset: JamlAssetKey | JamlAssetFile): string {
  const assetKey =
    asset in JAML_ASSET_FILES
      ? (asset as JamlAssetKey)
      : assetKeyByFileName[asset as JamlAssetFile];

  if (!assetKey) {
    throw new Error(`Unknown Jaml asset '${asset}'.`);
  }

  return customAssetBaseUrl
    ? joinAssetUrl(customAssetBaseUrl, JAML_ASSET_FILES[assetKey])
    : defaultAssetUrls[assetKey];
}
