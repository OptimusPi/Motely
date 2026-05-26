'use client'

import React from 'react'
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
 * + optional sort dropdown with floating pill label.
 */
export function JimboFilterBar({
  search,
  onSearchChange,
  searchPlaceholder = 'Search...',
  searchLabel = 'Search',
  sort,
  onSortChange,
  sortLabel = 'Sort By',
  sortOptions,
  className = '',
  style,
}: JimboFilterBarProps) {
  return (
    <div className={`j-filter-bar ${className}`} style={style}>
      {onSearchChange ? (
        <div className="j-filter-bar__field">
          <div className="j-filter-bar__pill">
            <JimboText size="xs">{searchLabel}</JimboText>
          </div>
          <div className="j-relative">
            <div className="j-filter-bar__search-icon">
              <SearchIcon />
            </div>
            <input
              type="text"
              value={search ?? ''}
              onChange={(e) => onSearchChange(e.target.value)}
              placeholder={searchPlaceholder}
              className="j-filter-bar__input"
            />
          </div>
        </div>
      ) : null}

      {sortOptions && onSortChange ? (
        <div className="j-filter-bar__field">
          <div className="j-filter-bar__pill">
            <JimboText size="xs">{sortLabel}</JimboText>
          </div>
          <div className="j-relative">
            <select
              value={sort ?? sortOptions[0]?.value}
              onChange={(e) => onSortChange(e.target.value)}
              className="j-filter-bar__select"
            >
              {sortOptions.map((opt) => (
                <option key={opt.value} value={opt.value}>{opt.label}</option>
              ))}
            </select>
            <div className="j-filter-bar__sort-icon">
              <SortIcon />
            </div>
          </div>
        </div>
      ) : null}
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
