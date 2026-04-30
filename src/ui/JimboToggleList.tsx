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
}

export function JimboToggleList({ items, onToggle, title }: JimboToggleListProps) {
  return (
    <JimboPanel>
      <div className="j-toggle-list">
        {title && <div className="j-toggle-list__title">{title}</div>}
        {items.map(item => (
          <button
            key={item.id}
            type="button"
            className="j-toggle-item"
            onClick={() => onToggle(item.id)}
          >
            <div className="j-toggle-check" data-on={item.on} />
            {item.label}
          </button>
        ))}
      </div>
    </JimboPanel>
  )
}
