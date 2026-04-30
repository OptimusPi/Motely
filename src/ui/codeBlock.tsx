'use client'

import React, { useState } from 'react'

export interface JimboCodeBlockProps {
  code: string
  language?: string
  filename?: string
  className?: string
}

export function JimboCodeBlock({ code, language, filename, className = '' }: JimboCodeBlockProps) {
  const [copied, setCopied] = useState(false)

  const copy = () => {
    void navigator.clipboard.writeText(code)
    setCopied(true)
    setTimeout(() => setCopied(false), 2000)
  }

  return (
    <div className={`j-code-block ${className}`}>
      <div className="j-code-block__header">
        <div className="j-code-block__meta">
          {filename && <span className="j-code-block__filename">{filename}</span>}
          {language && <span className="j-code-block__lang">{language}</span>}
        </div>
        <button
          onClick={copy}
          title="Copy"
          className="j-code-block__copy"
          data-copied={copied}
        >
          {copied
            ? <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round"><polyline points="20 6 9 17 4 12"/></svg>
            : <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round"><rect x="9" y="9" width="13" height="13" rx="2"/><path d="M5 15H4a2 2 0 0 1-2-2V4a2 2 0 0 1 2-2h9a2 2 0 0 1 2 2v1"/></svg>
          }
        </button>
      </div>
      <pre className="j-code-block__pre">
        <code>{code}</code>
      </pre>
    </div>
  )
}
