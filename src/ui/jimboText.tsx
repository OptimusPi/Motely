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
  | 'white'

export type JimboTextSize = 'micro' | 'label' | 'xs' | 'body' | 'sm' | 'md' | 'heading' | 'lg' | 'xl' | 'display'

export interface JimboTextProps extends React.HTMLAttributes<HTMLElement> {
  tone?: JimboTextTone
  size?: JimboTextSize
  /** Canonical Balatro drop shadow (1px right, 1px down, ${BLACK}cc). Default true. */
  shadow?: boolean
  /** Uppercase + spacing — Balatro button/pill label treatment. Default false. */
  uppercase?: boolean
  /** Wiggle effect for text characters. Default false. */
  dance?: boolean
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
  dance = false,
  letterSpacing,
  as: Tag = 'span',
  className = '',
  style,
  children,
  ...rest
}: JimboTextProps) {
  const sizeClass = `j-text--${size}`
  const toneClass = `j-text--${tone}`
  const shadowClass = shadow ? '' : 'j-text--no-shadow'
  const upperClass = uppercase ? 'j-text--upper' : ''
  const danceClass = dance ? 'j-text--dance-container' : ''

  const inlineStyle: React.CSSProperties = {}
  if (letterSpacing != null) {
    inlineStyle.letterSpacing = letterSpacing
  } else if (uppercase && letterSpacing == null) {
    inlineStyle.letterSpacing = 2
  }
  if (style) Object.assign(inlineStyle, style)

  let content = children
  if (dance && typeof children === 'string') {
    content = children.split('').map((char, i) => (
      <span
        key={i}
        className="j-font-dance-char"
        style={{ animationDelay: `${i * -0.15}s` }}
      >
        {char === ' ' ? '\u00A0' : char}
      </span>
    ))
  }

  return (
    <Tag
      className={`j-text ${sizeClass} ${toneClass} ${shadowClass} ${upperClass} ${danceClass} ${className}`.trim()}
      style={Object.keys(inlineStyle).length > 0 ? inlineStyle : undefined}
      {...rest}
    >
      {content}
    </Tag>
  )
}
