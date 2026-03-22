/** JSON shape from Motely AnalyzeSeed (camelCase via System.Text.Json). */
export interface ShopItemInfo {
  id: string
  name: string
  /** Packed Motely item bits (sprite / asset key). */
  value?: number
}

export interface PackInfo {
  type: string
  items: string[]
}

export interface AnteAnalysisInfo {
  ante: number
  boss: string
  voucher: string
  smallBlindTag: string
  bigBlindTag: string
  drawOrder: string
  shopQueue: ShopItemInfo[]
  packs: PackInfo[]
}

export interface SeedAnalysisInfo {
  seed: string
  deck: string
  stake: string
  erraticDeckComposition: string[]
  error?: string | null
  antes: AnteAnalysisInfo[]
}
