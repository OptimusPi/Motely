'use client'

import React, { useState } from 'react'
import { JimboText } from './jimboText.js'
import { JimboButton } from './panel.js'

export interface JimboCopyRowProps {
  value: string
  label?: string
}

/**
 * Inline copy-to-clipboard row: label + value chip + JimboButton.
 * The button is a real JimboButton — toggles red ("Copy") → green ("Copied")
 * for 1.5s after click. No raw button shell.
 */
export function JimboCopyRow({ value, label }: JimboCopyRowProps) {
  const [copied, setCopied] = useState(false)

  function copy() {
    navigator.clipboard.writeText(value).then(() => {
      setCopied(true)
      setTimeout(() => setCopied(false), 1500)
    })
  }

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
        <JimboButton
          tone={copied ? 'green' : 'red'}
          size="sm"
          onClick={copy}
        >
          {copied ? 'Copied' : 'Copy'}
        </JimboButton>
      </div>
    </div>
  )
}
