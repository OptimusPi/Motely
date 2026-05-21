'use client'

import React, { useState } from 'react'
import { JimboButton, type JimboTone } from './panel.js'

export interface JimboCopyButtonProps {
  /** Value placed on the clipboard when the button is clicked. */
  value: string
  /** Label while idle. Default "Copy". */
  label?: string
  /** Label briefly shown after a successful copy. Default "Copied". */
  copiedLabel?: string
  /** Button tone. Default blue for standalone copy actions; seed rows use
   *  JimboSeedCopyChip (WeeJoker harvest) instead. */
  tone?: JimboTone
  /** Button size. Default "sm". */
  size?: 'xs' | 'sm' | 'md' | 'lg'
  /** How long to display the copied label, in ms. Default 1500. */
  copiedDurationMs?: number
  /** Fired after the clipboard write resolves. */
  onCopy?: () => void
  className?: string
}

/**
 * The canonical copy-to-clipboard button. Single tone (red by default —
 * stays red through the click; only the label briefly swaps to "Copied").
 * Reusable everywhere a copy action lives: seed copy rows, code blocks,
 * filter sharing, etc. — instead of every consumer rolling its own
 * `<JimboButton onClick={...}>Copy</JimboButton>` + setTimeout dance.
 */
export function JimboCopyButton({
  value,
  label = 'Copy',
  copiedLabel = 'Copied',
  tone = 'blue',
  size = 'sm',
  copiedDurationMs = 1500,
  onCopy,
  className,
}: JimboCopyButtonProps) {
  const [copied, setCopied] = useState(false)

  function handleClick() {
    void navigator.clipboard.writeText(value).then(() => {
      setCopied(true)
      onCopy?.()
      window.setTimeout(() => setCopied(false), copiedDurationMs)
    })
  }

  return (
    <JimboButton
      tone={tone}
      size={size}
      onClick={handleClick}
      className={className}
    >
      {copied ? copiedLabel : label}
    </JimboButton>
  )
}
