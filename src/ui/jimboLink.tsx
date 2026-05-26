'use client'

import React from 'react'

export interface JimboLinkProps extends React.AnchorHTMLAttributes<HTMLAnchorElement> {
  /** External link: opens in new tab with rel="noopener noreferrer". Default: true. */
  external?: boolean
  children?: React.ReactNode
}

/**
 * Canonical Jimbo link primitive. The only sanctioned way to render an anchor
 * inside any Jimbo* / Jaml* component. Default styling matches the gold
 * Balatro link colour from `.j-link` in jimbo.css.
 */
export function JimboLink({
  external = true,
  className = '',
  children,
  ...anchorProps
}: JimboLinkProps) {
  const externalAttrs = external
    ? { target: '_blank', rel: 'noopener noreferrer' as const }
    : {}
  return (
    <a
      className={`j-link ${className}`.trim()}
      {...externalAttrs}
      {...anchorProps}
    >
      {children}
    </a>
  )
}
