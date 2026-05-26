'use client'

import React from 'react'

export interface JimboInsetProps extends React.HTMLAttributes<HTMLDivElement> {
  children?: React.ReactNode
}

/**
 * Recessed dark content area. Use for log output, recent finds,
 * console-like streams. All styling via `.j-inset` in jimbo.css.
 */
export function JimboInset({ children, className = '', ...props }: JimboInsetProps) {
  return (
    <div className={`j-inset ${className}`.trim()} {...props}>
      {children}
    </div>
  )
}
