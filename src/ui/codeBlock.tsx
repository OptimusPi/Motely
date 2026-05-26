'use client'

import React from 'react'
import { JimboText } from './jimboText.js'
import { JimboCopyButton } from './JimboCopyButton.js'

export interface JimboCodeBlockProps {
  code: string
  language?: string
  filename?: string
  className?: string
}

/**
 * Code block with filename + language chip + JimboCopyButton. Pure Jimbo
 * primitives end to end; the copy logic lives in JimboCopyButton.
 */
export function JimboCodeBlock({ code, language, filename, className = '' }: JimboCodeBlockProps) {
  const displayLanguage = filename?.toLowerCase().endsWith('.jaml')
    ? 'JAML'
    : language?.toLowerCase() === 'yaml'
      ? undefined
      : language

  return (
    <div className={`j-code-block ${className}`}>
      <div className="j-code-block__header">
        <div className="j-code-block__meta">
          {filename && (
            <JimboText size="xs" tone="grey" className="j-code-block__filename">
              {filename}
            </JimboText>
          )}
          {displayLanguage && (
            <span className="j-code-block__lang">
              <JimboText size="micro" tone="blue">{displayLanguage}</JimboText>
            </span>
          )}
        </div>
        <JimboCopyButton value={code} />
      </div>
      <pre className="j-code-block__pre">
        <code>{code}</code>
      </pre>
    </div>
  )
}
