'use client'

import React, { useState } from 'react'
import { JimboText } from './jimboText.js'

export interface JimboCopyRowProps {
  value: string
  label?: string
}

/**
 * Inline copy-to-clipboard row with label + value + button.
 * All styling via jimbo.css `.j-copy-row` classes.
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
        <button
          type="button"
          className="j-copy-row__btn"
          data-copied={copied}
          onClick={copy}
        >
          {copied ? 'Copied' : 'Copy'}
        </button>
      </div>
    </div>
  )
}
