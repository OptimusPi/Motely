'use client'

import React from 'react'
import { JimboButton } from './panel.js'
import { JimboSprite } from './sprites.js'
import { JimboText } from './jimboText.js'
import { JimboApp, JimboAppFooter } from './jimboApp.js'
import { JimboBalatroFooter } from './footer.js'
import { JimboSectionHeader, type JimboSectionTone } from './jimboSectionHeader.js'
import { JimboInfoCard, JimboInfoCardBody, JimboInfoCardTitle, JimboInfoCardSub, JimboInfoCardAside } from './jimboInfoCard.js'

export interface ShowcaseFilter {
  name: string
  author: string
  hits: string
  tone: JimboSectionTone
  sample: string[]
}

export interface ShowcaseRecentFind {
  seed: string
  filterName: string
  score: number
}

export interface ShowcaseLiveStats {
  searched: string
  matches: string
  speed: string
}

export interface ShowcaseMcpInfo {
  runtime: string
  engine: string
  features: string
}

export interface ShowcaseProps {
  title?: string
  subtitle?: string
  hotFilters?: ShowcaseFilter[]
  recentFinds?: ShowcaseRecentFind[]
  mcpInfo?: ShowcaseMcpInfo
  onNewSearch?: () => void
  onBrowseFilters?: () => void
  onFilterClick?: (filter: ShowcaseFilter, index: number) => void
}

/**
 * Landing/showcase screen — 320×568, NO SCROLL.
 * Every pixel accounted for. No flex stretching. No gaps.
 */
export function Showcase({
  title = 'Balatro',
  subtitle = 'Seed Curator',
  hotFilters = [],
  recentFinds = [],
  mcpInfo,
  onNewSearch,
  onBrowseFilters,
  onFilterClick,
}: ShowcaseProps) {
  return (
    <JimboApp>
      {/* Content — no flex-grow, no dead space, footer follows naturally */}
      <div style={{ padding: '12px 12px 8px', display: 'flex', flexDirection: 'column', gap: 6 }}>
        {/* Compact title */}
        <div className="j-text-center">
          <JimboText size="lg" tone="gold">{title}</JimboText>
          <JimboText size="micro" tone="grey" style={{ letterSpacing: 3 }}>{subtitle}</JimboText>
        </div>

        {/* Engine strip — single line */}
        {mcpInfo && (
          <div className="j-flex j-justify-between" style={{
            padding: '3px 8px',
            background: 'var(--j-dark-grey)', borderRadius: 4,
            border: '1px solid var(--j-panel-edge)',
          }}>
            <JimboText size="micro" tone="purple">{mcpInfo.engine}</JimboText>
            <JimboText size="micro" tone="grey">{mcpInfo.features}</JimboText>
          </div>
        )}

        {/* Hot Filters */}
        {hotFilters.length > 0 && (
          <>
            <JimboSectionHeader label="Filters" tone="blue" />
            <div className="j-flex-col" style={{ gap: 4 }}>
              {hotFilters.slice(0, 4).map((f, i) => (
                <JimboInfoCard
                  key={i}
                  tone={f.tone}
                  onClick={() => onFilterClick?.(f, i)}
                  style={{ cursor: onFilterClick ? 'pointer' : undefined }}
                >
                  <div className="j-flex j-gap-xs">
                    {f.sample.slice(0, 2).map((name, j) => (
                      <div key={j} style={{ width: 22, height: 28, display: 'flex', alignItems: 'center', justifyContent: 'center' }}>
                        <JimboSprite name={name} width={20} />
                      </div>
                    ))}
                  </div>
                  <JimboInfoCardBody>
                    <JimboInfoCardTitle>{f.name}</JimboInfoCardTitle>
                    <JimboInfoCardSub>by {f.author}</JimboInfoCardSub>
                  </JimboInfoCardBody>
                  <JimboInfoCardAside>
                    <JimboText size="xs" tone={f.tone === 'gold' ? 'gold' : f.tone}>{f.hits}</JimboText>
                  </JimboInfoCardAside>
                </JimboInfoCard>
              ))}
            </div>
          </>
        )}

        {/* Recent finds — compact */}
        {recentFinds.length > 0 && (
          <>
            <JimboSectionHeader label="Recent" tone="green" />
            <div style={{ lineHeight: 1.5 }}>
              {recentFinds.slice(0, 3).map((r, i) => (
                <div key={i} className="j-flex j-gap-sm">
                  <JimboText size="micro" tone="gold">{r.seed}</JimboText>
                  <JimboText size="micro" tone="grey">{r.filterName}</JimboText>
                  {r.score > 0 && <JimboText size="micro" tone="green">+{r.score}</JimboText>}
                </div>
              ))}
            </div>
          </>
        )}
      </div>

      <JimboAppFooter>
        <JimboButton tone="green" fullWidth size="lg" onClick={onNewSearch}>New Search</JimboButton>
        <JimboButton tone="blue"  fullWidth size="lg" onClick={onBrowseFilters}>Browse Filters</JimboButton>
        <JimboBalatroFooter />
      </JimboAppFooter>
    </JimboApp>
  )
}
