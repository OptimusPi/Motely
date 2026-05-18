'use client'

import React from 'react'
import { JimboText } from '../ui/jimboText.js'
import { JimboBadge } from '../ui/JimboBadge.js'

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
 * capability indicators. All styling via jimbo.css `.j-motely-badge`.
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
      <span className={`j-motely-badge ${className}`} style={style}>
        <JimboText size="xs" tone="grey">Initializing…</JimboText>
      </span>
    )
  }

  const resolved = caps?.version ?? version ?? '?'
  const simd = caps?.simd
  const threads = caps?.threads

  if (minimal) {
    return (
      <span className={`j-motely-badge ${className}`} style={style}>
        <JimboText size="xs" tone="grey">v{resolved}</JimboText>
        {simd ? <JimboBadge size="sm" tone="blue" title="SIMD enabled">SIMD</JimboBadge> : null}
        {threads ? <JimboBadge size="sm" tone="green" title="Multi-threaded">MT</JimboBadge> : null}
      </span>
    )
  }

  return (
    <div className={`j-motely-badge j-motely-badge--chip ${className}`} style={style}>
      <JimboText size="xs" tone="gold">motely v{resolved}</JimboText>
      {simd ? <JimboBadge size="sm" tone="blue" title="SIMD enabled">SIMD</JimboBadge> : null}
      {threads ? <JimboBadge size="sm" tone="green" title="Multi-threaded">MT</JimboBadge> : null}
    </div>
  )
}
