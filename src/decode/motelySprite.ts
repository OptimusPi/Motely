import { decodeMotelyItem, type MotelyRenderableCategory } from "./motelyItemDecoder.js";
import { getSpriteData, SHEET_META } from "../sprites/spriteMapper.js";
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
