'use client'

import React, { useState } from 'react'
import { JimboColorOption } from './tokens.js'

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
    <div
      className={'rounded-xl overflow-hidden flex flex-col border-2 ' + className}
      style={{ backgroundColor: JimboColorOption.DARKEST, borderColor: JimboColorOption.PANEL_EDGE, boxShadow: '0 3px 0 0 rgba(0,0,0,0.5)' }}
    >
      <div style={{ padding: '0.5rem 1rem', display: 'flex', alignItems: 'center', justifyContent: 'space-between', borderBottom: `1px solid ${JimboColorOption.INNER_BORDER}` }}>
        <div style={{ display: 'flex', gap: '0.5rem', alignItems: 'center' }}>
          {filename && <span style={{ fontSize: 10, opacity: 0.6 }}>{filename}</span>}
          {language && <span style={{ fontSize: 9, padding: '1px 6px', borderRadius: 3, background: 'rgba(0,0,0,0.4)', color: '#60a5fa' }}>{language}</span>}
        </div>
        <button
          onClick={copy}
          title="Copy"
          style={{ padding: 4, background: 'none', border: 'none', cursor: 'pointer', color: copied ? '#4ade80' : 'rgba(255,255,255,0.5)', display: 'flex' }}
        >
          {copied
            ? <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round"><polyline points="20 6 9 17 4 12"/></svg>
            : <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round"><rect x="9" y="9" width="13" height="13" rx="2"/><path d="M5 15H4a2 2 0 0 1-2-2V4a2 2 0 0 1 2-2h9a2 2 0 0 1 2 2v1"/></svg>
          }
        </button>
      </div>
      <pre style={{ padding: '1rem', overflowX: 'auto', fontFamily: 'monospace', fontSize: '0.875rem', lineHeight: 1.6, color: '#f6f0d5', margin: 0 }}>
        <code>{code}</code>
      </pre>
    </div>
  )
}
