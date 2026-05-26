'use client'

import React from 'react'
import { JimboPanel, JimboButton } from './panel.js'
import { JimboText } from './jimboText.js'
import { JimboStack, JimboRow } from './layout.js'

export type JimboConfirmTone = 'red' | 'green' | 'orange' | 'blue'

export interface JimboConfirmPromptProps {
  open: boolean
  message: React.ReactNode
  confirmLabel?: string
  cancelLabel?: string
  confirmTone?: JimboConfirmTone
  onConfirm?: () => void
  onCancel?: () => void
}

/** Small modal with a question + confirm/cancel. Defaults to destructive (red). */
export function JimboConfirmPrompt({
  open,
  message,
  confirmLabel = 'yes',
  cancelLabel = 'cancel',
  confirmTone = 'red',
  onConfirm,
  onCancel,
}: JimboConfirmPromptProps) {
  if (!open) return null
  return (
    <div className="j-modal-overlay">
      <JimboPanel>
        <JimboStack gap="md">
          <JimboText size="md" tone="white">{message}</JimboText>
          <JimboRow gap="sm" justify="end">
            <JimboButton tone="grey" size="sm" onClick={onCancel}>{cancelLabel}</JimboButton>
            <JimboButton tone={confirmTone} size="sm" onClick={onConfirm}>{confirmLabel}</JimboButton>
          </JimboRow>
        </JimboStack>
      </JimboPanel>
    </div>
  )
}
