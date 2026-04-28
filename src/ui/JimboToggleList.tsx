import React from 'react'
import { JimboPanel, JimboButton } from './panel.js'

export interface ToggleItem {
  id: string;
  label: string;
  on: boolean;
}

export interface JimboToggleListProps {
  items: ToggleItem[];
  onToggle: (id: string) => void;
  title?: string;
}

export function JimboToggleList({ items, onToggle, title }: JimboToggleListProps) {
  return (
    <JimboPanel>
      <div style={{ display: 'flex', flexDirection: 'column', gap: 4 }}>
        {title && <div style={{ fontSize: 12, color: 'var(--c-grey)', marginBottom: 4, fontFamily: "'m6x11plus', monospace", textTransform: 'uppercase', letterSpacing: '0.04em' }}>{title}</div>}
        {items.map(item => (
          <button
            key={item.id}
            type="button"
            onClick={() => onToggle(item.id)}
            style={{ 
              justifyContent: 'flex-start', 
              textAlign: 'left', 
              display: 'flex', 
              alignItems: 'center', 
              gap: '8px',
              padding: '6px 8px',
              backgroundColor: 'rgba(255, 255, 255, 0.05)',
              border: '1px solid rgba(0, 0, 0, 0.2)',
              borderRadius: 4,
              cursor: 'pointer',
              fontFamily: "'m6x11plus', monospace",
              color: 'var(--c-white)',
              textTransform: 'uppercase',
              letterSpacing: '0.04em'
            }}
          >
            <div style={{
              width: 10, height: 10, flexShrink: 0,
              background: item.on ? 'var(--c-orange)' : 'var(--c-darkest)',
              border: '1px solid var(--c-dark-grey)',
              boxShadow: 'inset 0 1px 2px rgba(0,0,0,0.5)'
            }} />
            {item.label}
          </button>
        ))}
      </div>
    </JimboPanel>
  )
}
