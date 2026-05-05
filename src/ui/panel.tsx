'use client'

import React, { useState, memo } from 'react'
import { useSway } from './hooks.js'
import { JimboText, type JimboTextSize } from './jimboText.js'

// ─── Panel ───────────────────────────────────────────────────────────────────

export interface JimboPanelProps extends React.HTMLAttributes<HTMLDivElement> {
  sway?: boolean
  onBack?: () => void
  hideBack?: boolean
}
export const JimboPanel = memo(({
  children, className = '', sway = false, onBack, hideBack = false, style, ...props
}: JimboPanelProps) => {
  const panelRef = useSway(sway)

  return (
    <div
      ref={panelRef}
      className={`j-panel ${className}`}
      style={style}
      {...props}
    >
      <div className="j-panel__body">{children}</div>
      {onBack && !hideBack && (
        <div className="j-panel__back">
          <JimboBackButton onClick={onBack} />
        </div>
      )}
    </div>
  )
})
JimboPanel.displayName = 'JimboPanel'

export interface JimboInnerPanelProps extends React.HTMLAttributes<HTMLDivElement> { }

export const JimboInnerPanel = memo(({ children, className = '', style, ...props }: JimboInnerPanelProps) => (
  <div
    className={`j-inner-panel ${className}`}
    style={style}
    {...props}
  >
    {children}
  </div>
))
JimboInnerPanel.displayName = 'JimboInnerPanel'

// ─── JimboButton ──────────────────────────────────────────────────────────────
// Canonical flat 2D Balatro-style button.
// Tones are purely CSS-driven via j-btn--{tone} classes in jimbo.css.
// No JS color maps. No TONE_PAIRS. Respect the design tokens.

export type JimboTone = 'orange' | 'red' | 'blue' | 'green' | 'tarot' | 'planet' | 'spectral' | 'grey'

export interface JimboButtonProps {
  tone?: JimboTone
  size?: 'xs' | 'sm' | 'md' | 'lg'
  fullWidth?: boolean
  disabled?: boolean
  uppercase?: boolean
  onClick?: () => void
  style?: React.CSSProperties
  className?: string
  children?: React.ReactNode
}

export function JimboButton({
  tone = 'orange', size = 'md', fullWidth = false, disabled = false, uppercase = false, onClick, style, className = '', children,
}: JimboButtonProps) {
  const textSize: JimboTextSize = size === 'xs' ? 'xs' : size === 'sm' ? 'sm' : size === 'lg' ? 'lg' : 'md'

  return (
    <button
      type="button"
      className={`j-btn j-btn--${tone} j-btn--${size} ${fullWidth ? 'j-btn--full' : ''} ${disabled ? 'j-btn--disabled' : ''} ${className}`}
      disabled={disabled}
      onClick={onClick}
      style={style}
    >
      <div className="j-btn__face">
        <JimboText size={textSize} uppercase={uppercase}>{children}</JimboText>
      </div>
    </button>
  )
}

export function JimboBackButton({ onClick }: { onClick?: () => void }) {
  return (
    <div className="j-flex j-justify-center j-w-full" style={{ padding: '4px 0' }}>
      <JimboButton tone="orange" size="md" fullWidth onClick={onClick}>Back</JimboButton>
    </div>
  )
}

// ─── Modal ───────────────────────────────────────────────────────────────────

export interface JimboModalProps {
  children: React.ReactNode
  open: boolean
  onClose: () => void
  title?: string
  className?: string
}

export function JimboModal({ children, open, onClose, title, className }: JimboModalProps) {
  if (!open) return null

  return (
    <div
      className="j-modal-overlay"
      onClick={onClose}
    >
      <JimboPanel
        onBack={onClose}
        className={`j-modal ${className ?? ''}`}
        onClick={(e: React.MouseEvent) => e.stopPropagation()}
      >
        {title && <JimboText as="h2" size="lg" className="j-modal__title">{title}</JimboText>}
        {children}
      </JimboPanel>
    </div>
  )
}
