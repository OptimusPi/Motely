import React from 'react'
import { JimboPanel } from './panel.js'

export interface ToggleItem {
  id: string;
  label: string;
  on: boolean;
}

export interface JimboToggleListProps {
  items: ToggleItem[];
  onToggle: (id: string) => void;
  title?: string;
  variant?: 'row' | 'stacked';
}

export function JimboToggleList({ items, onToggle, title, variant = 'row' }: JimboToggleListProps) {
  return (
    <JimboPanel>
      <div className={`j-toggle-list j-toggle-list--${variant}`}>
        {title && <div className="j-toggle-list__title">{title}</div>}
        {items.map(item => (
          <button
            key={item.id}
            type="button"
            className="j-toggle-item"
            data-variant={variant}
            aria-pressed={item.on}
            onClick={() => onToggle(item.id)}
          >
            <span className="j-toggle-item__label">{item.label}</span>
            <span className="j-toggle-check" data-on={item.on} />
          </button>
        ))}
      </div>
    </JimboPanel>
  )
}
