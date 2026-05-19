'use client'

import React from 'react'
import { JimboText } from './jimboText.js'
import { JimboCopyButton } from './JimboCopyButton.js'

export interface JimboCopyRowProps {
  value: string
  label?: string
}

/**
 * Inline copy-to-clipboard row: label + value chip + JimboCopyButton.
 * Wraps the canonical copy button; the row is responsible for the layout,
 * the button owns the copy logic.
 */
export function JimboCopyRow({ value, label }: JimboCopyRowProps) {
  return (
    <div className="j-copy-row">
      {label && (
        <JimboText size="xs" tone="grey" className="j-copy-row__label">
          {label}
        </JimboText>
      )}
      <div className="j-copy-row__field">
        <div className="j-copy-row__value">
          <JimboText size="sm">{value}</JimboText>
        </div>
        <JimboCopyButton value={value} />
      </div>
    </div>
  )
}
