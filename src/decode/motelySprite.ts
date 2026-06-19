import { decodeMotelyItem, type MotelyRenderableCategory } from "./motelyItemDecoder.js";
import { getSpriteData, SHEET_META } from "../sprites/spriteMapper.js";
import { RANK_MAP, SUIT_MAP } from "../sprites/spriteData.js";
import { resolveJamlAssetUrl } from "../assets.js";

export interface MotelySpriteData {
  atlasPath: string;
  gridCol: number;
  gridRow: number;
  gridCols: number;
  gridRows: number;
  displayName: string;
  category: MotelyRenderableCategory;
}

/**
 * Given a raw motely-wasm item value (which may be a bitpacked integer or raw MotelyItemType),
 * resolves it to a sprite atlas path and grid coordinates for rendering.
 */
export function motelyItemToSprite(rawValue: number): MotelySpriteData | null {
  const decoded = decodeMotelyItem(rawValue);
  if (!decoded) return null;

  if (decoded.category === "playing" && decoded.rank && decoded.suit) {
    const col = RANK_MAP[decoded.rank];
    const row = SUIT_MAP[decoded.suit];
    if (col !== undefined && row !== undefined) {
      return {
        atlasPath: resolveJamlAssetUrl('deck'),
        gridCol: col,
        gridRow: row,
        gridCols: 13,
        gridRows: 4,
        displayName: decoded.displayName,
        category: "playing"
      };
    }
  }

  const sprite = getSpriteData(decoded.displayName);
  if (!sprite) return null;

  const meta = SHEET_META[sprite.type];
  if (!meta) return null;

  return {
    atlasPath: resolveJamlAssetUrl(meta.assetKey),
    gridCol: sprite.pos.x,
    gridRow: sprite.pos.y,
    gridCols: meta.cols,
    gridRows: meta.rows,
    displayName: decoded.displayName,
    category: decoded.category
  };
}

/**
 * Resolves a sprite by name and category without needing a Motely integer.
 */
export function getMotelySpriteByName(name: string, category: MotelyRenderableCategory = "unknown"): MotelySpriteData | null {
  if (category === "playing") {
    // Attempt to parse "Rank of Suit"
    const match = /^(.*?)\s+of\s+(.*?)$/i.exec(name);
    if (match) {
        const rank = match[1];
        const suit = match[2];
        const col = RANK_MAP[rank];
        const row = SUIT_MAP[suit];
        if (col !== undefined && row !== undefined) {
          return {
            atlasPath: resolveJamlAssetUrl('deck'),
            gridCol: col,
            gridRow: row,
            gridCols: 13,
            gridRows: 4,
            displayName: name,
            category: "playing"
          };
        }
    }
  }

  const sprite = getSpriteData(name);
  if (!sprite) return null;

  const meta = SHEET_META[sprite.type];
  if (!meta) return null;

  return {
    atlasPath: resolveJamlAssetUrl(meta.assetKey),
    gridCol: sprite.pos.x,
    gridRow: sprite.pos.y,
    gridCols: meta.cols,
    gridRows: meta.rows,
    displayName: name,
    category
  };
}
