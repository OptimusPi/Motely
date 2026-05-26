'use client'

import React from 'react'

export type JimboGap = 'xs' | 'sm' | 'md' | 'lg' | 'xl'
export type JimboJustify = 'start' | 'center' | 'end' | 'between'

export interface JimboStackProps extends React.HTMLAttributes<HTMLDivElement> {
  gap?: JimboGap
  children?: React.ReactNode
}

export function JimboStack({ gap = 'md', className = '', children, ...rest }: JimboStackProps) {
  return (
    <div className={`j-stack j-stack--${gap} ${className}`.trim()} {...rest}>
      {children}
    </div>
  )
}

export interface JimboRowProps extends React.HTMLAttributes<HTMLDivElement> {
  gap?: JimboGap
  justify?: JimboJustify
  children?: React.ReactNode
}

export function JimboRow({ gap = 'md', justify, className = '', children, ...rest }: JimboRowProps) {
  const justifyClass = justify ? `j-row--${justify}` : ''
  return (
    <div className={`j-row j-row--${gap} ${justifyClass} ${className}`.trim()} {...rest}>
      {children}
    </div>
  )
}

export interface JimboDividerProps {
  orientation?: 'horizontal' | 'vertical'
  className?: string
}

export function JimboDivider({ orientation = 'horizontal', className = '' }: JimboDividerProps) {
  const orient = orientation === 'vertical' ? 'j-divider--vert' : ''
  return <hr className={`j-divider ${orient} ${className}`.trim()} />
}
