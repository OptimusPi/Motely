'use client'

import React, { useState, useEffect, useRef, memo } from 'react'
import { JimboColorOption, JIMBO_ANIMATIONS } from './tokens.js'
import { useSway, useDelayedVisibility, useDOMMagneticTilt } from './hooks.js'
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

export interface JimboInnerPanelProps extends React.HTMLAttributes<HTMLDivElement> {}

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
// Two-layer: separate shadow div (3px south + 1px east) that disappears on press.
// Press translates the face onto the shadow. No gradients, no hover color change.

const JIMBO_TONE_PAIRS: Record<string, [string, string]> = {
  orange: [JimboColorOption.ORANGE, JimboColorOption.DARK_ORANGE],
  red:    [JimboColorOption.RED,    JimboColorOption.DARK_RED],
  blue:   [JimboColorOption.BLUE,   JimboColorOption.DARK_BLUE],
  green:  [JimboColorOption.GREEN,  JimboColorOption.DARK_GREEN],
  tarot:  ['#9e74ce', '#5e437e'],
  planet: ['#00a7ca', '#00657c'],
  spectral: ['#2e76fd', '#14449e'],
  grey:   ['#888888', '#555555'],
  gold:   ['#f1c40f', '#d35400'],
}

export type JimboTone = 'orange' | 'red' | 'blue' | 'green' | 'tarot' | 'planet' | 'spectral' | 'grey' | 'gold'

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
  const [pressed, setPressed] = useState(false)
  const [fg, sh] = JIMBO_TONE_PAIRS[tone] ?? JIMBO_TONE_PAIRS.orange
  const textSize: JimboTextSize = size === 'xs' ? 'xs' : size === 'sm' ? 'sm' : size === 'lg' ? 'lg' : 'md'
  const { handlers, tiltStyle } = useDOMMagneticTilt(!disabled)

  return (
    <div
      className={`j-btn j-btn--${tone} j-btn--${size} ${fullWidth ? 'j-btn--full' : ''} ${disabled ? 'j-btn--disabled' : ''} ${className}`}
      data-pressed={pressed}
      onMouseDown={() => { if (!disabled) setPressed(true) }}
      onMouseUp={() => setPressed(false)}
      onMouseLeave={(e) => { setPressed(false) }}
      onTouchStart={() => { if (!disabled) setPressed(true) }}
      onTouchEnd={() => setPressed(false)}
      onClick={() => { if (!disabled) onClick?.() }}
      onPointerEnter={handlers.onPointerEnter}
      onPointerMove={handlers.onPointerMove}
      onPointerLeave={handlers.onPointerLeave}
      style={style}
    >
      <div style={{ ...tiltStyle, width: '100%' }}>
        <div className="j-btn__shadow" style={{ background: sh }} />
        <div className="j-btn__face" style={{ background: fg }}>
          <JimboText size={textSize} uppercase={uppercase}>{children}</JimboText>
        </div>
      </div>
    </div>
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
        sway
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
