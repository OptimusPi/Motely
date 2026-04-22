'use client'

import React from 'react'
import { JimboColorOption } from '../ui/tokens.js'
import { JimboText } from '../ui/jimboText.js'

export interface MotelyCapabilities {
  version: string
  simd?: boolean
  threads?: boolean
}

export interface MotelyVersionBadgeProps {
  /**
   * Runtime capabilities from `motely.getCapabilities()`. If omitted, the
   * consumer can pass a static `version` (typically from motely-wasm's
   * own package.json) and the component will render without the SIMD /
   * threads indicators.
   */
  caps?: MotelyCapabilities | null
  /** Static fallback version when `caps` is null/undefined. */
  version?: string
  /** Compact single-line badge instead of the labelled chip. Default false. */
  minimal?: boolean
  /** Loading placeholder (shown while caps are being fetched). */
  loading?: boolean
  className?: string
  style?: React.CSSProperties
}

/**
 * Badge showing the loaded motely-wasm version + optional SIMD / threads
 * capability indicators. Ported from weejoker.app with no dependency on
 * weejoker's lib/api — the consumer owns capability fetching and passes
 * the result in.
 */
export function MotelyVersionBadge({
  caps,
  version,
  minimal = false,
  loading = false,
  className = '',
  style,
}: MotelyVersionBadgeProps) {
  if (loading) {
    return (
      <span className={className} style={style}>
        <JimboText size="xs" tone="grey">Initializing…</JimboText>
      </span>
    )
  }

  const resolved = caps?.version ?? version ?? '?'
  const simd = caps?.simd
  const threads = caps?.threads

  if (minimal) {
    return (
      <span
        className={className}
        style={{ display: 'inline-flex', alignItems: 'center', gap: 6, ...style }}
      >
        <JimboText size="xs" tone="grey">v{resolved}</JimboText>
        {simd ? (
          <JimboText size="xs" tone="blue" title="SIMD enabled">⚡</JimboText>
        ) : null}
        {threads ? (
          <JimboText size="xs" tone="green" title="Multi-threaded">🧵</JimboText>
        ) : null}
      </span>
    )
  }

  return (
    <div
      className={className}
      style={{
        display: 'inline-flex',
        alignItems: 'center',
        gap: 6,
        padding: '3px 8px',
        borderRadius: 4,
        background: JimboColorOption.DARKEST,
        border: `1px solid ${JimboColorOption.PANEL_EDGE}`,
        ...style,
      }}
    >
      <JimboText size="xs" tone="gold" uppercase>motely v{resolved}</JimboText>
      {simd ? <JimboText size="xs" tone="blue" title="SIMD enabled">⚡</JimboText> : null}
      {threads ? <JimboText size="xs" tone="green" title="Multi-threaded">🧵</JimboText> : null}
    </div>
  )
}
