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

/**
 * Vertical tabs = a column of red JimboButtons. The active tab gets a red
 * arrow pointing right toward the panel content, bouncing on the X axis with
 * the same gravity easing. Buttons hold still; the arrow is the only motion.
 */
export function JimboVerticalTabs({ tabs, activeTab, onTabChange, className = '', style }: JimboTabsProps) {
  return (
    <div className={`j-vtabs ${className}`} style={style}>
      {tabs.map((tab) => {
        const active = activeTab === tab.id
        return (
          <div className="j-vtab" data-active={active} key={tab.id}>
            <JimboButton tone="red" size="md" fullWidth onClick={() => onTabChange(tab.id)}>
              {tab.label}
            </JimboButton>
            <div className="j-vtab__indicator" data-active={active} aria-hidden>
              <svg width={10} height={14} viewBox="0 0 10 14">
                {/* Right-pointing triangle — apex points at the panel. */}
                <polygon points="0,0 10,7 0,14" />
              </svg>
            </div>
          </div>
        )
      })}
    </div>
  )
}
