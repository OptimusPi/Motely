'use client'

import React from 'react'
import { JimboColorOption } from './tokens.js'

export type JimboTextTone =
  | 'default'
  | 'mult'
  | 'chips'
  | 'gold'
  | 'green'
  | 'red'
  | 'blue'
  | 'orange'
  | 'purple'
  | 'grey'

export type JimboTextSize = 'xs' | 'sm' | 'md' | 'lg' | 'xl'

const TONE_COLOR: Record<JimboTextTone, string> = {
  default: JimboColorOption.WHITE,
  mult:    JimboColorOption.RED,
  chips:   JimboColorOption.BLUE,
  gold:    JimboColorOption.GOLD_TEXT,
  green:   JimboColorOption.GREEN_TEXT,
  red:     JimboColorOption.RED,
  blue:    JimboColorOption.BLUE,
  orange:  JimboColorOption.ORANGE_TEXT,
  purple:  JimboColorOption.PURPLE,
  grey:    JimboColorOption.GREY,
}

const SIZE_PX: Record<JimboTextSize, number> = {
  xs: 10,
  sm: 12,
  md: 14,
  lg: 18,
  xl: 24,
}

export interface JimboTextProps extends React.HTMLAttributes<HTMLElement> {
  tone?: JimboTextTone
  size?: JimboTextSize
  /** Canonical Balatro drop shadow (1px right, 1px down, ${BLACK}cc). Default true. */
  shadow?: boolean
  /** Uppercase + spacing — Balatro button/pill label treatment. Default false. */
  uppercase?: boolean
  /** Letter-spacing override; defaults depend on uppercase prop. */
  letterSpacing?: number | string
  as?: 'span' | 'p' | 'div' | 'h1' | 'h2' | 'h3' | 'h4' | 'h5' | 'h6' | 'label'
  children?: React.ReactNode
}

/**
 * Canonical pixel-font text wrapper. Uses `m6x11plus` as its family and
 * applies the authentic Balatro drop shadow by default. Prefer this over
 * hand-rolling `fontFamily: 'm6x11plus, monospace'` + `textShadow` strings
 * throughout consumers — it keeps the styling drift-free.
 *
 * Requires `import 'jaml-ui/fonts.css'` somewhere in the consumer bundle
 * so the @font-face declaration lands (font is base64-embedded, no
 * runtime fetch).
 */
export function JimboText({
  tone = 'default',
  size = 'md',
  shadow = true,
  uppercase = false,
  letterSpacing,
  as: Tag = 'span',
  style,
  children,
  ...rest
}: JimboTextProps) {
  const resolvedLetterSpacing =
    letterSpacing ?? (uppercase ? 2 : undefined)
  return (
    <Tag
      style={{
        fontFamily: "'m6x11plus', 'Courier New', monospace",
        fontSize: SIZE_PX[size],
        color: TONE_COLOR[tone],
        textShadow: shadow ? `1px 1px 0 ${JimboColorOption.BLACK}cc` : 'none',
        textTransform: uppercase ? 'uppercase' : 'none',
        letterSpacing: resolvedLetterSpacing,
        lineHeight: 1.2,
        ...style,
      }}
      {...rest}
    >
      {children}
    </Tag>
  )
}
