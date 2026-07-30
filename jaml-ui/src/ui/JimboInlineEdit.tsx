'use client'

import React from 'react'

export type JimboInlineEditSize = 'xs' | 'sm' | 'md' | 'lg'
export type JimboInlineEditTone = 'white' | 'gold' | 'grey'

export interface JimboInlineEditProps
  extends Omit<React.InputHTMLAttributes<HTMLInputElement>, 'size'> {
  /** Visual size — echoes JimboText size scale. */
  size?: JimboInlineEditSize
  tone?: JimboInlineEditTone
  /** Render at reduced opacity (e.g. description-style sub-text). */
  dim?: boolean
}

/**
 * Borderless transparent input that reads as inline text until focused —
 * for editable titles, bylines, or descriptions inside cards/panels.
 * Focus chrome only shows on keyboard navigation so mouse use stays clean.
 */
export const JimboInlineEdit = React.forwardRef<HTMLInputElement, JimboInlineEditProps>(
  function JimboInlineEdit(
    { size = 'md', tone = 'white', dim = false, className = '', ...rest },
    ref,
  ) {
    const classes = [
      'j-inline-edit',
      `j-inline-edit--${size}`,
      `j-inline-edit--${tone}`,
      dim ? 'j-inline-edit--dim' : null,
      className || null,
    ]
      .filter(Boolean)
      .join(' ')
    return <input ref={ref} type="text" className={classes} {...rest} />
  },
)
