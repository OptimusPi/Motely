export const SHOP_DECKS = [
  'Red',
  'Blue',
  'Yellow',
  'Green',
  'Black',
  'Magic',
  'Nebula',
  'Ghost',
  'Abandoned',
  'Checkered',
  'Zodiac',
  'Painted',
  'Anaglyph',
  'Plasma',
  'Erratic',
] as const

export const SHOP_STAKES = [
  'White',
  'Red',
  'Green',
  'Black',
  'Blue',
  'Purple',
  'Orange',
  'Gold',
] as const

export type ShopDeck = (typeof SHOP_DECKS)[number]
export type ShopStake = (typeof SHOP_STAKES)[number]

/** One row in the shop list (TS uses CardType enum; Motely uses string ids). */
export type ShopStreamRow = Readonly<{ index: number; type: string; name: string }>

export const shopStreamStyles = {
  root: {
    background: '#0d0d12',
    color: '#ccc',
    minHeight: '100vh',
    padding: 20,
    paddingBottom: 48,
    fontFamily: 'ui-monospace, monospace',
    fontSize: 13,
    lineHeight: 1.45,
  } as const,
  h1: { color: '#e8e8e8', fontSize: 18, margin: '0 0 6px 0' },
  sub: { color: '#666', fontSize: 12, marginBottom: 16, maxWidth: 720 },
  row: {
    display: 'flex',
    flexWrap: 'wrap' as const,
    gap: 10,
    alignItems: 'center' as const,
    marginBottom: 14,
  },
  label: { color: '#888', fontSize: 11 },
  input: {
    background: '#16161c',
    border: '1px solid #2a2a32',
    color: '#ddd',
    padding: '6px 10px',
    fontSize: 13,
    borderRadius: 4,
    minWidth: 200,
  },
  select: {
    background: '#16161c',
    border: '1px solid #2a2a32',
    color: '#ddd',
    padding: '6px 10px',
    fontSize: 13,
    borderRadius: 4,
  },
  btn: {
    background: '#1e3a2e',
    border: '1px solid #2a5a44',
    color: '#8fd4a8',
    padding: '8px 16px',
    fontSize: 13,
    borderRadius: 4,
    cursor: 'pointer',
  },
  btnMuted: {
    background: '#2a2520',
    border: '1px solid #4a4035',
    color: '#c4b8a8',
  },
  err: { color: '#e07070', marginTop: 12, whiteSpace: 'pre-wrap' as const, maxWidth: 900 },
  shop: {
    margin: 0,
    paddingLeft: 18,
    color: '#aaa',
    maxHeight: 'min(60vh, 520px)',
    overflowY: 'auto' as const,
  },
  meta: { color: '#555', fontSize: 11, marginTop: 20 },
} as const
