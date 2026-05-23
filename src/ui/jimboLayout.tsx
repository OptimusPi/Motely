'use client'

import React from 'react'

export type JimboLayoutGap = 'xs' | 'sm' | 'md' | 'lg' | 'xl'
export type JimboLayoutAlign = 'start' | 'center' | 'end' | 'stretch'
export type JimboLayoutJustify = 'start' | 'center' | 'end' | 'between'

interface BaseLayoutProps extends React.HTMLAttributes<HTMLDivElement> {
  gap?: JimboLayoutGap
  align?: JimboLayoutAlign
  justify?: JimboLayoutJustify
}

export interface JimboStackProps extends BaseLayoutProps {}

/**
 * Vertical stack — CSS grid column-flow with token-aligned gap.
 * Grid (not flex) because the 320×568 canvas is fixed: we compose with named
 * tracks instead of guess-and-checking flexbox flow.
 */
export function JimboStack({
  gap,
  align,
  justify,
  className = '',
  ...props
}: JimboStackProps) {
  const classes = [
    'j-stack',
    gap && `j-stack--gap-${gap}`,
    align && `j-stack--align-${align}`,
    justify && `j-stack--justify-${justify}`,
    className,
  ].filter(Boolean).join(' ')
  return <div className={classes} {...props} />
}

export interface JimboRowProps extends BaseLayoutProps {
  wrap?: boolean
}

/**
 * Horizontal row — CSS grid row-flow with token-aligned gap.
 * Grid (not flex) for the same reason as JimboStack — composition over flow.
 */
export function JimboRow({
  gap,
  align,
  justify,
  wrap = false,
  className = '',
  ...props
}: JimboRowProps) {
  const classes = [
    'j-row',
    gap && `j-row--gap-${gap}`,
    align && `j-row--align-${align}`,
    justify && `j-row--justify-${justify}`,
    wrap && 'j-row--wrap',
    className,
  ].filter(Boolean).join(' ')
  return <div className={classes} {...props} />
}
