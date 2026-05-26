'use client'

import React from 'react'

export type JimboToastTone = 'green' | 'red' | 'blue' | 'orange'

export interface JimboToastProps {
  message: React.ReactNode
  tone?: JimboToastTone
  durationMs?: number
  onDismiss?: () => void
}

export function JimboToast({ message, tone, durationMs = 2000, onDismiss }: JimboToastProps) {
  React.useEffect(() => {
    if (!onDismiss) return
    const t = window.setTimeout(onDismiss, durationMs)
    return () => window.clearTimeout(t)
  }, [durationMs, onDismiss])

  return (
    <div className="j-toast-host">
      <div className={`j-toast ${tone ? `j-toast--${tone}` : ''}`.trim()}>{message}</div>
    </div>
  )
}

export interface JimboTooltipProps {
  children: React.ReactNode
  content: React.ReactNode
  open?: boolean
  className?: string
}

export function JimboTooltip({ children, content, open, className = '' }: JimboTooltipProps) {
  const [hover, setHover] = React.useState(false)
  const visible = open ?? hover
  return (
    <span
      className={`j-tooltip ${className}`.trim()}
      onMouseEnter={() => setHover(true)}
      onMouseLeave={() => setHover(false)}
      onFocus={() => setHover(true)}
      onBlur={() => setHover(false)}
    >
      {children}
      {visible && content && (
        <span className="j-tooltip__bubble" role="tooltip">
          {content}
          <span className="j-tooltip__pointer" />
        </span>
      )}
    </span>
  )
}

export interface JimboErrorBlockProps {
  title?: React.ReactNode
  children: React.ReactNode
  className?: string
  style?: React.CSSProperties
}

export function JimboErrorBlock({ title, children, className = '', style }: JimboErrorBlockProps) {
  return (
    <div className={`j-error-block ${className}`.trim()} style={style}>
      {title && <div className="j-error-block__title">{title}</div>}
      {children}
    </div>
  )
}

export type JimboProgressTone = 'blue' | 'red' | 'green' | 'orange' | 'gold'

export interface JimboProgressBarProps {
  value: number
  max?: number
  tone?: JimboProgressTone
  caption?: React.ReactNode
  className?: string
  style?: React.CSSProperties
}

export function JimboProgressBar({
  value,
  max = 100,
  tone = 'blue',
  caption,
  className = '',
  style,
}: JimboProgressBarProps) {
  const pct = max === 0 ? 0 : Math.max(0, Math.min(100, (value / max) * 100))
  return (
    <div className={`j-progress ${className}`.trim()} style={style}>
      <div
        className="j-progress__track"
        role="progressbar"
        aria-valuenow={value}
        aria-valuemax={max}
      >
        <div
          className={`j-progress__fill j-progress__fill--${tone}`}
          style={{ width: `${pct}%` }}
        />
      </div>
      {caption && <div className="j-progress__caption">{caption}</div>}
    </div>
  )
}
