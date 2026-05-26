'use client'

import * as React from 'react'
import { JimboButton } from './panel.js'

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
 * Horizontal tabs = a row of red JimboButtons. The active tab gets a red
 * triangle above it that bounces on the Y axis with gravity easing. Only
 * the indicator arrow animates; the buttons themselves hold still and look
 * identical (the bouncing arrow IS the active-state signal).
 *
 * Every tab is a JimboButton — no custom button shells, no raw `<button>`.
 */
export function JimboTabs({ tabs, activeTab, onTabChange, className = '', style }: JimboTabsProps) {
  return (
    <div className={`j-tabs ${className}`} style={style}>
      {tabs.map((tab) => {
        const active = activeTab === tab.id
        return (
          <div className="j-tab" data-active={active} key={tab.id}>
            <div className="j-tab__indicator" data-active={active} aria-hidden>
              <svg width={14} height={10} viewBox="0 0 14 10">
                {/* Down-pointing triangle — apex points at the button below. */}
                <polygon points="7,10 0,0 14,0" />
              </svg>
            </div>
            <JimboButton tone="red" size="sm" onClick={() => onTabChange(tab.id)}>
              {tab.label}
            </JimboButton>
          </div>
        )
      })}
    </div>
  )
}
