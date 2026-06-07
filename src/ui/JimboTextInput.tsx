'use client'

import React from 'react'

export interface JimboTextInputProps extends React.InputHTMLAttributes<HTMLInputElement> {
  invalid?: boolean
}

export const JimboTextInput = React.forwardRef<HTMLInputElement, JimboTextInputProps>(function JimboTextInput(
  { className = '', invalid = false, 'aria-invalid': ariaInvalid, ...props },
  ref,
) {
  return (
    <input
      ref={ref}
      className={`j-text-input ${className}`.trim()}
      aria-invalid={ariaInvalid ?? invalid}
      data-invalid={invalid}
      {...props}
    />
  )
})