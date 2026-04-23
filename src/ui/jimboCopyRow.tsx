'use client'

import React, { useState } from 'react'
import { JimboColorOption } from './tokens.js'
import { JimboText } from './jimboText.js'

export interface JimboCopyRowProps {
  value: string
  label?: string
}

export function JimboCopyRow({ value, label }: JimboCopyRowProps) {
  const [copied, setCopied] = useState(false)
  const C = JimboColorOption

  function copy() {
    navigator.clipboard.writeText(value).then(() => {
      setCopied(true)
      setTimeout(() => setCopied(false), 1500)
    })
  }

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 4 }}>
      {label && (
        <JimboText size="xs" tone="grey" uppercase style={{ letterSpacing: 2 }}>
          {label}
        </JimboText>
      )}
      <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
        <div style={{
          flex: 1,
          padding: '6px 10px',
          background: C.DARKEST,
          border: `2px solid ${C.PANEL_EDGE}`,
          borderRadius: 4,
          wordBreak: 'break-all',
        }}>
          <JimboText size="sm">{value}</JimboText>
        </div>
        <button
          type="button"
          onClick={copy}
          style={{
            fontFamily: "'m6x11plus', 'Courier New', monospace",
            fontSize: 11,
            letterSpacing: 2,
            color: copied ? C.GREEN_TEXT : C.GOLD_TEXT,
            background: copied ? 'rgba(53,189,134,0.12)' : 'rgba(228,182,67,0.12)',
            border: `1px solid ${copied ? C.GREEN_TEXT : C.GOLD_TEXT}`,
            borderRadius: 4,
            padding: '4px 12px',
            cursor: 'pointer',
            flexShrink: 0,
            transition: 'color 0.15s, background 0.15s, border-color 0.15s',
            textTransform: 'uppercase',
          }}
        >
          {copied ? 'Copied' : 'Copy'}
        </button>
      </div>
    </div>
  )
}
