/**
 * Balatro design tokens — colors eyedropped from actual game pixels.
 * Do NOT replace with Lua HEX values; the game's shader pipeline transforms them.
 *
 * IMPORTANT: For DOM components, use CSS custom properties (--j-red, etc.)
 * from jimbo.css. Only use these JS constants for contexts that cannot use
 * CSS — such as R3F/Canvas, inline SVG fills, or imperative animation APIs.
 */

export const JimboColorOption = {
  RED: '#ff4c40',
  BLUE: '#0093ff',
  GREEN: '#429f79',
  ORANGE: '#ff9800',
  GOLD: '#e4b643',
  PURPLE: '#9e74ce',

  DARK_RED: '#a02721',
  DARK_BLUE: '#0057a1',
  DARK_ORANGE: '#a05b00',
  DARK_GREEN: '#215f46',
  DARK_PURPLE: '#5e437e',

  DARK_GREY: '#3a5055',
  DARKEST: '#1e2b2d',
  GREY: '#708386',
  TEAL_GREY: '#404c4e',

  PANEL_EDGE: '#1e2e32',
  INNER_BORDER: '#334461',

  BORDER_SILVER: '#b9c2d2',
  BORDER_SOUTH: '#777e89',

  GOLD_TEXT: '#e4b643',
  GREEN_TEXT: '#35bd86',
  ORANGE_TEXT: '#ff8f00',
  WHITE: '#ffffff',
  BLACK: '#000000',

  TAROT_BUTTON: '#9e74ce',
  PLANET_BUTTON: '#00a7ca',
  SPECTRAL_BUTTON: '#2e76fd',
  TAROT_BUTTON_DARK: '#5e437e',
  PLANET_BUTTON_DARK: '#00657c',
  SPECTRAL_BUTTON_DARK: '#14449e',
} as const

export type JimboPaletteColor = keyof typeof JimboColorOption

/** Convert hex to rgba for inline styles in Canvas/SVG/R3F contexts. */
export function withAlpha(hex: string, alpha: number): string {
  const clean = hex.replace('#', '')
  const r = parseInt(clean.slice(0, 2), 16)
  const g = parseInt(clean.slice(2, 4), 16)
  const b = parseInt(clean.slice(4, 6), 16)
  return `rgba(${r}, ${g}, ${b}, ${alpha})`
}

export const JIMBO_ANIMATIONS = {
  JUICE_UP_SCALE: 1.05,
  JUICE_DOWN_SCALE: 1.0,
  JUICE_DURATION: 150,
  JUICE_EASING: 'cubic-bezier(0.34, 1.56, 0.64, 1)',
  SWAY_AMOUNT: 1.5,
  SWAY_DURATION: 4000,
  PRESS_TRANSLATE_Y: 2,
  PRESS_DURATION: 50,
  CARD_TILT_MAX: 6,
  MENU_SINK_DURATION: 200,
  MENU_RISE_DURATION: 300,
  MENU_ORBIT_DURATION: 320,
  LETTER_POP_RATE: 3,
  LETTER_BUMP_RATE: 2.666,
} as const

