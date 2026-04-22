'use client'

import React from 'react'
import { JimboColorOption } from './tokens.js'
import { JimboText } from './jimboText.js'

export interface JimboFilterSortOption {
  value: string
  label: string
}

export interface JimboFilterBarProps {
  search?: string
  onSearchChange?: (query: string) => void
  searchPlaceholder?: string
  searchLabel?: string

  sort?: string
  onSortChange?: (value: string) => void
  sortLabel?: string
  sortOptions?: JimboFilterSortOption[]

  className?: string
  style?: React.CSSProperties
}

/**
 * Generic Balatro-styled filter row: search input with floating pill label
 * + optional sort dropdown with floating pill label. Adapted from
 * weejoker's FilterBar — no hardcoded sort options, no lucide dep.
 *
 * Pass `sortOptions` to show the sort side; omit to show search only.
 */
export function JimboFilterBar({
  search,
  onSearchChange,
  searchPlaceholder = 'SEARCH...',
  searchLabel = 'Search',
  sort,
  onSortChange,
  sortLabel = 'Sort By',
  sortOptions,
  className = '',
  style,
}: JimboFilterBarProps) {
  return (
    <div
      className={className}
      style={{
        display: 'flex',
        gap: 24,
        padding: 16,
        backgroundColor: JimboColorOption.DARK_GREY,
        border: `4px solid ${JimboColorOption.BORDER_SILVER}`,
        boxShadow: `0 3px 0 0 ${JimboColorOption.BORDER_SOUTH}`,
        borderRadius: 12,
        position: 'relative',
        flexWrap: 'wrap',
        ...style,
      }}
    >
      {onSearchChange ? (
        <FloatingLabelField label={searchLabel}>
          <div style={{ position: 'relative' }}>
            <div style={{ position: 'absolute', left: 0, top: 0, bottom: 0, width: 48, display: 'flex', alignItems: 'center', justifyContent: 'center', pointerEvents: 'none', color: JimboColorOption.BLUE, zIndex: 1 }}>
              <SearchIcon />
            </div>
            <input
              type="text"
              value={search ?? ''}
              onChange={(e) => onSearchChange(e.target.value)}
              placeholder={searchPlaceholder}
              style={{
                width: '100%',
                paddingLeft: 48,
                paddingRight: 16,
                paddingTop: 14,
                paddingBottom: 14,
                backgroundColor: JimboColorOption.DARKEST,
                border: 'none',
                borderBottom: `4px solid ${JimboColorOption.PANEL_EDGE}`,
                borderRadius: 8,
                color: JimboColorOption.WHITE,
                fontFamily: "'m6x11plus', 'Courier New', monospace",
                fontSize: 20,
                letterSpacing: 2,
                textTransform: 'uppercase',
                outline: 'none',
              }}
            />
          </div>
        </FloatingLabelField>
      ) : null}

      {sortOptions && onSortChange ? (
        <FloatingLabelField label={sortLabel}>
          <div style={{ position: 'relative' }}>
            <select
              value={sort ?? sortOptions[0]?.value}
              onChange={(e) => onSortChange(e.target.value)}
              style={{
                appearance: 'none',
                WebkitAppearance: 'none',
                MozAppearance: 'none',
                backgroundColor: JimboColorOption.ORANGE,
                color: JimboColorOption.WHITE,
                border: 'none',
                borderBottom: `4px solid ${JimboColorOption.DARK_ORANGE}`,
                borderRadius: 8,
                cursor: 'pointer',
                fontFamily: "'m6x11plus', 'Courier New', monospace",
                fontSize: 18,
                letterSpacing: 2,
                textTransform: 'uppercase',
                padding: '14px 48px 14px 24px',
                minWidth: 200,
                textAlign: 'center',
                outline: 'none',
              }}
            >
              {sortOptions.map((opt) => (
                <option key={opt.value} value={opt.value}>{opt.label}</option>
              ))}
            </select>
            <div style={{ position: 'absolute', right: 16, top: '50%', transform: 'translateY(-50%)', pointerEvents: 'none', color: JimboColorOption.WHITE, opacity: 0.85 }}>
              <SortIcon />
            </div>
          </div>
        </FloatingLabelField>
      ) : null}
    </div>
  )
}

function FloatingLabelField({ label, children }: { label: string; children: React.ReactNode }) {
  return (
    <div style={{ flex: 1, minWidth: 200, position: 'relative', marginTop: 10 }}>
      <div
        style={{
          position: 'absolute',
          top: -14,
          left: 16,
          backgroundColor: JimboColorOption.RED,
          border: `2px solid ${JimboColorOption.DARK_RED}`,
          borderRadius: 6,
          padding: '4px 12px',
          zIndex: 2,
        }}
      >
        <JimboText size="xs" uppercase>{label}</JimboText>
      </div>
      {children}
    </div>
  )
}

function SearchIcon() {
  return (
    <svg width={24} height={24} viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth={3} strokeLinecap="round" strokeLinejoin="round" aria-hidden>
      <circle cx={11} cy={11} r={8} />
      <line x1={21} y1={21} x2={16.65} y2={16.65} />
    </svg>
  )
}

function SortIcon() {
  return (
    <svg width={20} height={20} viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth={2.5} strokeLinecap="round" strokeLinejoin="round" aria-hidden>
      <polyline points="7 4 7 20" />
      <polyline points="3 8 7 4 11 8" />
      <polyline points="17 20 17 4" />
      <polyline points="21 16 17 20 13 16" />
    </svg>
  )
}
