'use client'

import React from 'react'

export interface JimboListItemProps extends React.ButtonHTMLAttributes<HTMLButtonElement> {
  /** Visual + a11y active state. Renders the selected/hover palette. */
  active?: boolean
}

/**
 * Clickable list row. Use anywhere a vertical stack of selectable rows is
 * needed — filter browsers, seed lists, saved-search picker, etc. Composes
 * `.j-list-item` from jimbo.css; pass extra layout inside via children.
 */
export const JimboListItem = React.forwardRef<HTMLButtonElement, JimboListItemProps>(
  function JimboListItem({ active = false, className = '', children, ...rest }, ref) {
    return (
      <button
        ref={ref}
        type="button"
        className={`j-list-item ${className}`.trim()}
        data-active={active}
        {...rest}
      >
        {children}
      </button>
    )
  },
)
