'use client'

import React, { useState } from 'react'
import { JimboColorOption, JIMBO_ANIMATIONS } from './tokens.js'
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
 * tab. Ported from JAMMY's jimbo-ui/Tabs.tsx — triangle attaches to each
 * button and animates only on the active one.
 */
export function JimboTabs({ tabs, activeTab, onTabChange, className = '', style }: JimboTabsProps) {
  return (
    <>
      <div
        className={className}
        style={{ display: 'flex', gap: 8, alignItems: 'flex-end', flexWrap: 'wrap', ...style }}
      >
        {tabs.map((tab) => (
          <TabButton
            key={tab.id}
            label={tab.label}
            active={activeTab === tab.id}
            onClick={() => onTabChange(tab.id)}
          />
        ))}
      </div>
      <style>{JIMBO_BOUNCE_KEYFRAMES}</style>
    </>
  )
}

const JIMBO_BOUNCE_KEYFRAMES = `
  @keyframes jimbo-bounce {
    0%, 100% { transform: translateY(0); }
    50%      { transform: translateY(-3px); }
  }
`

function TabButton({ label, active, onClick }: { label: string; active: boolean; onClick: () => void }) {
  const [pressed, setPressed] = useState(false)
  return (
    <div style={{ position: 'relative', display: 'flex', flexDirection: 'column', alignItems: 'center' }}>
      <div
        style={{
          marginBottom: 4,
          visibility: active ? 'visible' : 'hidden',
          animation: active ? 'jimbo-bounce 0.8s cubic-bezier(0.68, 0, 0.68, 1) infinite' : 'none',
        }}
        aria-hidden
      >
        <svg width={14} height={10} viewBox="0 0 14 10" fill={JimboColorOption.RED}>
          <polygon points="7,10 0,0 14,0" />
        </svg>
      </div>
      <button
        type="button"
        onClick={onClick}
        onMouseDown={() => setPressed(true)}
        onMouseUp={() => setPressed(false)}
        onMouseLeave={() => setPressed(false)}
        onTouchStart={() => setPressed(true)}
        onTouchEnd={() => setPressed(false)}
        style={{
          border: 'none',
          cursor: 'pointer',
          borderRadius: 8,
          padding: '8px 16px',
          backgroundColor: JimboColorOption.RED,
          transform: pressed ? `translateY(${JIMBO_ANIMATIONS.PRESS_TRANSLATE_Y}px)` : 'translateY(0)',
          boxShadow: pressed ? 'none' : `0 ${JIMBO_ANIMATIONS.PRESS_TRANSLATE_Y}px 0 0 ${JimboColorOption.BLACK}80`,
          transition: `transform ${JIMBO_ANIMATIONS.PRESS_DURATION}ms ease, box-shadow ${JIMBO_ANIMATIONS.PRESS_DURATION}ms ease`,
        }}
      >
        <JimboText size="sm" uppercase>{label}</JimboText>
      </button>
    </div>
  )
}

/**
 * Vertical tab strip — rotated labels (writing-mode) for space efficiency.
 * Ported from JAMMY's JimboVerticalTabs. Typical use: inline on the left
 * side of a content panel to pick between content categories
 * (e.g. JOKERS / CONSUMABLES / VOUCHERS).
 */
export function JimboVerticalTabs({ tabs, activeTab, onTabChange, className = '', style }: JimboTabsProps) {
  return (
    <div
      className={className}
      style={{ display: 'flex', flexDirection: 'column', gap: 4, ...style }}
    >
      {tabs.map((tab) => {
        const isActive = activeTab === tab.id
        return (
          <button
            key={tab.id}
            type="button"
            onClick={() => onTabChange(tab.id)}
            style={{
              border: 'none',
              cursor: 'pointer',
              borderRadius: '8px 0 0 8px',
              padding: '16px 8px',
              backgroundColor: isActive ? JimboColorOption.DARK_GREY : JimboColorOption.INNER_BORDER,
              writingMode: 'vertical-rl',
              textOrientation: 'mixed',
              transform: 'rotate(180deg)',
              transition: 'background-color 120ms ease',
            }}
          >
            <JimboText size="sm" uppercase tone={isActive ? 'default' : 'grey'}>
              {tab.label}
            </JimboText>
          </button>
        )
      })}
    </div>
  )
}
