'use client'

import React from 'react'

export interface JimboInsetProps extends React.HTMLAttributes<HTMLDivElement> {
  children: React.ReactNode
}

/**
 * Dark recessed content area — for log output, recent finds, etc.
 * All styling via jimbo.css `.j-inset` class.
 */
export function JimboInset({ children, className = '', ...props }: JimboInsetProps) {
  return (
    <div className={`j-inset ${className}`} {...props}>
      {children}
    </div>
  )
}
