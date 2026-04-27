'use client'

import React, { useState, useEffect, useRef, memo } from 'react'
import { JimboColorOption, JIMBO_ANIMATIONS } from './tokens.js'
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
  const panelRef = useRef<HTMLDivElement>(null)

  useEffect(() => {
    if (!sway || !panelRef.current) return
    let frame: number
    const start = Date.now()
    const el = panelRef.current
    const tick = () => {
      const t = ((Date.now() - start) % JIMBO_ANIMATIONS.SWAY_DURATION) / JIMBO_ANIMATIONS.SWAY_DURATION * Math.PI * 2
      el.style.transform = `translate(${Math.sin(t) * JIMBO_ANIMATIONS.SWAY_AMOUNT * 0.3}px, ${Math.sin(t * 0.8) * JIMBO_ANIMATIONS.SWAY_AMOUNT}px)`
      frame = requestAnimationFrame(tick)
    }
    frame = requestAnimationFrame(tick)
    return () => { cancelAnimationFrame(frame); el.style.transform = '' }
  }, [sway])

  return (
    <div
      ref={panelRef}
      className={'rounded-xl p-4 flex flex-col items-stretch overflow-hidden ' + className}
      style={{
        backgroundColor: JimboColorOption.DARK_GREY,
        border: `3px solid ${JimboColorOption.BORDER_SILVER}`,
        boxShadow: `0 3px 0 0 ${JimboColorOption.BORDER_SOUTH}`,
        ...style,
      }}
      {...props}
    >
      <div className="flex-1 overflow-auto">{children}</div>
      {onBack && !hideBack && (
        <div className="mt-4 pt-2 shrink-0">
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
    className={'rounded-lg p-3 ' + className}
    style={{ backgroundColor: JimboColorOption.INNER_BORDER, border: `2px solid ${JimboColorOption.PANEL_EDGE}`, ...style }}
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
  gold:   [JimboColorOption.GOLD,   '#8a6a1e'],
  grey:   [JimboColorOption.DARK_GREY, JimboColorOption.DARKEST],
}

export type JimboTone = 'orange' | 'red' | 'blue' | 'green' | 'gold' | 'grey'

export interface JimboButtonProps {
  tone?: JimboTone
  size?: 'xs' | 'sm' | 'md' | 'lg'
  fullWidth?: boolean
  disabled?: boolean
  uppercase?: boolean
  onClick?: () => void
  style?: React.CSSProperties
  children?: React.ReactNode
}

export function JimboButton({
  tone = 'orange', size = 'md', fullWidth = false, disabled = false, uppercase = false, onClick, style, children,
}: JimboButtonProps) {
  const [pressed, setPressed] = useState(false)
  const [fg, sh] = JIMBO_TONE_PAIRS[tone] ?? JIMBO_TONE_PAIRS.orange
  const pad = size === 'xs' ? '2px 8px' : size === 'sm' ? '4px 10px' : size === 'lg' ? '10px 18px' : '6px 14px'
  const textSize: JimboTextSize = size === 'xs' ? 'xs' : size === 'sm' ? 'sm' : size === 'lg' ? 'lg' : 'md'

  return (
    <div
      onMouseDown={() => { if (!disabled) setPressed(true) }}
      onMouseUp={() => setPressed(false)}
      onMouseLeave={() => setPressed(false)}
      onTouchStart={() => { if (!disabled) setPressed(true) }}
      onTouchEnd={() => setPressed(false)}
      onClick={() => { if (!disabled) onClick?.() }}
      style={{ display: 'inline-block', width: fullWidth ? '100%' : undefined, position: 'relative', cursor: disabled ? 'not-allowed' : 'pointer', userSelect: 'none', opacity: disabled ? 0.55 : 1, ...style }}
    >
      <div style={{ position: 'absolute', left: 1, top: 3, right: -1, bottom: -3, background: sh, borderRadius: 6, opacity: pressed ? 0 : 1 }} />
      <div style={{
        position: 'relative', background: fg, borderRadius: 6, padding: pad,
        transform: pressed ? 'translate(1px, 3px)' : 'translate(0,0)',
        transition: 'transform 55ms linear',
        textAlign: 'center',
      }}>
        <JimboText size={textSize} uppercase={uppercase}>{children}</JimboText>
      </div>
    </div>
  )
}

export function JimboBackButton({ onClick }: { onClick?: () => void }) {
  return (
    <div style={{ display: 'flex', justifyContent: 'center', width: '100%', padding: '4px 0' }}>
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
  const [visible, setVisible] = useState(open)
  const [opacity, setOpacity] = useState(open ? 1 : 0)

  useEffect(() => {
    if (open) {
      setVisible(true)
      requestAnimationFrame(() => setOpacity(1))
    } else {
      setOpacity(0)
      const t = setTimeout(() => setVisible(false), JIMBO_ANIMATIONS.MENU_SINK_DURATION)
      return () => clearTimeout(t)
    }
  }, [open])

  if (!visible) return null

  return (
    <div
      style={{ position: 'fixed', inset: 0, zIndex: 50, display: 'flex', alignItems: 'center', justifyContent: 'center', padding: '1rem', background: 'rgba(0,0,0,0.7)', opacity, transition: `opacity ${JIMBO_ANIMATIONS.MENU_SINK_DURATION}ms ease` }}
      onClick={onClose}
    >
      <JimboPanel
        sway
        onBack={onClose}
        className={'w-full flex flex-col max-h-[90vh] ' + (className ?? 'max-w-lg')}
        onClick={(e: React.MouseEvent) => e.stopPropagation()}
      >
        {title && <JimboText as="h2" size="lg" style={{ textAlign: 'center', margin: '0 0 1rem' }}>{title}</JimboText>}
        {children}
      </JimboPanel>
    </div>
  )
}
