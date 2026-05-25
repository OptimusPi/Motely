'use client'

import { memo } from 'react'

export type JimboTone = 'orange' | 'red' | 'blue' | 'green' | 'tarot' | 'planet' | 'spectral' | 'grey'

export type JimboButtonSize = 'xs' | 'sm' | 'md' | 'lg'

export interface JimboButtonProps {
  tone?: JimboTone
  size?: JimboButtonSize
  disabled?: boolean
  fullWidth?: boolean
  onClick?: () => void
  children: React.ReactNode
  className?: string
  style?: React.CSSProperties
}

export const JimboButton = memo(function JimboButton({
  tone = 'orange',
  size = 'md',
  disabled = false,
  fullWidth = false,
  onClick,
  children,
  className = '',
  style,
}: JimboButtonProps) {
  const classes = [
    'j-btn',
    `j-btn--${size}`,
    `j-btn--${tone}`,
    fullWidth ? 'j-btn--full' : '',
    disabled ? 'j-btn--disabled' : '',
    className,
  ]
    .filter(Boolean)
    .join(' ')

  return (
    <button
      type="button"
      className={classes}
      disabled={disabled}
      onClick={disabled ? undefined : onClick}
      style={style}
    >
      <span className="j-btn__face">{children}</span>
    </button>
  )
})

export function JimboBackButton({ onClick, size = 'sm' }: { onClick?: () => void; size?: JimboButtonSize }) {
  return <JimboButton tone="orange" size={size} fullWidth onClick={onClick}>Back</JimboButton>
}
