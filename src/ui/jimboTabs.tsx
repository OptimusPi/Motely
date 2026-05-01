'use client'

import * as React from 'react'
import { useState } from 'react'
import { JimboText } from './jimboText.js'

export interface JimboTabItem {
  id: string
  label: string
}

export interface JimboTabsProps {
  tabs: JimboTabItem[]
  activeTab: string
  onTabChange: (tabId: string) => void
  className?: string
  style?: React.CSSProperties
}

/**
 * Horizontal tab navigation with bouncing triangle indicator on the active
 * tab. Triangle attaches to each button and animates only on the active one.
 */
export function JimboTabs({ tabs, activeTab, onTabChange, className = '', style }: JimboTabsProps) {
  return (
    <div className={`j-tabs ${className}`} style={style}>
      {tabs.map((tab) => (
        <TabButton
          key={tab.id}
          label={tab.label}
          active={activeTab === tab.id}
          onClick={() => onTabChange(tab.id)}
        />
      ))}
    </div>
  )
}

function TabButton({ label, active, onClick }: { label: string; active: boolean; onClick: () => void }) {
  const [pressed, setPressed] = useState(false)

  return (
    <div className="j-tab">
      <div
        className="j-tab__indicator"
        data-active={active}
        aria-hidden
      >
        <svg width={14} height={10} viewBox="0 0 14 10" fill="var(--j-red)">
          <polygon points="7,10 0,0 14,0" />
        </svg>
      </div>
      <button
        type="button"
        className="j-tab__btn"
        data-pressed={pressed}
        onClick={onClick}
        onMouseDown={() => setPressed(true)}
        onMouseUp={() => setPressed(false)}
        onMouseEnter={() => {}}
        onMouseLeave={() => { setPressed(false) }}
        onTouchStart={() => setPressed(true)}
        onTouchEnd={() => setPressed(false)}
      >
        <JimboText size="sm">{label}</JimboText>
      </button>
    </div>
  )
}

/**
 * Vertical tab strip — rotated labels (writing-mode) for space efficiency.
 */
export function JimboVerticalTabs({ tabs, activeTab, onTabChange, className = '', style }: JimboTabsProps) {
  return (
    <div className={`j-vtabs ${className}`} style={style}>
      {tabs.map((tab) => {
        const isActive = activeTab === tab.id
        return (
          <button
            key={tab.id}
            type="button"
            className="j-vtab"
            onClick={() => onTabChange(tab.id)}
          >
            <JimboText size="sm" tone={isActive ? 'default' : 'grey'}>{tab.label}</JimboText>
          </button>
        )
      })}
    </div>
  )
}
