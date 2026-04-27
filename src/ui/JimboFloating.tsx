import React from 'react'

export interface JimboFloatingProps {
  anchor?: 'top-left' | 'top-right' | 'bottom-left' | 'bottom-right' | 'top-center' | 'bottom-center'
  offset?: number
  zIndex?: number
  children: React.ReactNode
}

export function JimboFloating({ anchor = 'top-right', offset = 12, zIndex = 20, children }: JimboFloatingProps) {
  const pos: React.CSSProperties = { position: 'absolute', zIndex };

  if (anchor.includes('top')) pos.top = offset;
  if (anchor.includes('bottom')) pos.bottom = offset;

  if (anchor.includes('left')) pos.left = offset;
  if (anchor.includes('right')) pos.right = offset;
  if (anchor.includes('center')) {
    pos.left = '50%';
    pos.transform = 'translateX(-50%)';
  }

  return (
    <div style={pos}>
      {children}
    </div>
  )
}
