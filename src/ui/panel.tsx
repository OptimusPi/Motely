'use client'

import React, { memo } from 'react'
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

export type JimboInnerPanelProps = React.HTMLAttributes<HTMLDivElement>;

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

export interface JimboButtonProps extends Omit<React.ButtonHTMLAttributes<HTMLButtonElement>, 'onClick'> {
  tone?: JimboTone
  size?: 'xs' | 'sm' | 'md' | 'lg'
  fullWidth?: boolean
  disabled?: boolean
  onClick?: () => void
  children?: React.ReactNode
}

export function JimboButton({
  tone = 'orange', size = 'md', fullWidth = false, disabled = false, onClick, style, className = '', children, ...buttonProps
}: JimboButtonProps) {
  const textSize: JimboTextSize = size === 'xs' ? 'xs' : size === 'sm' ? 'sm' : size === 'lg' ? 'lg' : 'md'

  return (
    <button
      type="button"
      className={`j-btn j-btn--${tone} j-btn--${size} ${fullWidth ? 'j-btn--full' : ''} ${disabled ? 'j-btn--disabled' : ''} ${className}`}
      disabled={disabled}
      onClick={onClick}
      style={style}
      {...buttonProps}
    >
      <div className="j-btn__face">
        <JimboText size={textSize}>{children}</JimboText>
      </div>
    </button>
  )
}

// Compact Back button. Default size 'sm' — the slab-tall 'md' Back was eating
// real estate inside modals where it's auto-injected by JimboModal.
export function JimboBackButton({ onClick, size = 'sm' }: { onClick?: () => void; size?: 'sm' | 'md' | 'lg' }) {
  return (
    <div className="j-back-btn-wrap j-flex j-justify-center j-w-full">
      <JimboButton tone="orange" size={size} fullWidth onClick={onClick} className="j-back-btn">Back</JimboButton>
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
  showBack?: boolean
}

export function JimboModal({ children, open, onClose, title, className, showBack = true }: JimboModalProps) {
  if (!open) return null

  return (
    <div className="j-modal-overlay">
      <JimboPanel
        onBack={showBack ? onClose : undefined}
        className={`j-modal ${className ?? ''}`}
      >
        {title && (
          <div className="j-modal__title-wrap" aria-hidden={false}>
            <JimboText as="h2" size="lg" className="j-modal__title">{title}</JimboText>
          </div>
        )}
        {children}
      </JimboPanel>
    </div>
  )
}
