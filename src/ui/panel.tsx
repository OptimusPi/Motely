'use client'

import React, { memo } from 'react'
import { useSway } from './hooks.js'
import { JimboText } from './jimboText.js'
import { JimboBackButton } from './JimboButton.js'

// JimboButton + JimboBackButton + JimboTone live in JimboButton.tsx (R3F).
// Re-exported here for back-compat with any code that imported from './panel'.
export { JimboButton, JimboBackButton, type JimboTone, type JimboButtonProps, type JimboButtonSize } from './JimboButton.js'

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
