'use client'

import React from 'react'
import { JimboColorOption } from './tokens.js'
import { JimboButton } from './panel.js'
import { JimboSprite } from './sprites.js'

export interface ShowcaseFilter {
  name: string
  author: string
  hits: string
  tone: 'blue' | 'red' | 'gold' | 'green'
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

export interface ShowcaseProps {
  hotFilters?: ShowcaseFilter[]
  recentFinds?: ShowcaseRecentFind[]
  stats?: ShowcaseLiveStats
  onNewSearch?: () => void
  onBrowseFilters?: () => void
  onBack?: () => void
}

const TONE_COLOR: Record<ShowcaseFilter['tone'], string> = {
  blue: JimboColorOption.BLUE,
  red:  JimboColorOption.RED,
  gold: JimboColorOption.GOLD,
  green: JimboColorOption.GREEN,
}

const DEFAULT_STATS: ShowcaseLiveStats = { searched: '0', matches: '0', speed: '0' }

/**
 * Landing/showcase screen for the seed curator.
 * All styling via jimbo.css `.j-showcase` classes — zero inline styles.
 */
export function Showcase({
  hotFilters = [],
  recentFinds = [],
  stats = DEFAULT_STATS,
  onNewSearch,
  onBrowseFilters,
  onBack,
}: ShowcaseProps) {
  const C = JimboColorOption

  return (
    <div className="j-showcase">
      <div className="j-showcase__scroll">

        {/* Wordmark */}
        <div className="j-showcase__wordmark">
          <div className="j-showcase__wordmark-title">Balatro</div>
          <div className="j-showcase__wordmark-sub">Seed · Curator</div>
        </div>

        {/* Live stats */}
        <div className="j-showcase__stats">
          {([
            [stats.searched, 'searched'],
            [stats.matches,  'matches'],
            [stats.speed,    'speed'],
          ] as const).map(([n, l]) => (
            <div key={l}>
              <div className="j-showcase__stat-value">{n}</div>
              <div className="j-showcase__stat-label">{l}</div>
            </div>
          ))}
        </div>

        {/* Hot filters header */}
        <div className="j-showcase__section-header">
          <div className="j-showcase__section-tag" style={{ background: C.BLUE }}>Hot Filters</div>
          <div className="j-showcase__section-rule" style={{ background: `${C.BLUE}55` }} />
        </div>

        {/* Filter cards */}
        <div className="j-showcase__filter-list">
          {hotFilters.map((f, i) => {
            const tColor = TONE_COLOR[f.tone]
            return (
              <div key={i} className="j-showcase__filter-card" style={{ border: `2px solid ${tColor}` }}>
                <div className="j-showcase__filter-sprites">
                  {f.sample.map((name, j) => (
                    <div key={j} className="j-showcase__filter-sprite">
                      <JimboSprite name={name} width={28} />
                    </div>
                  ))}
                </div>
                <div className="j-showcase__filter-info">
                  <div className="j-showcase__filter-name">{f.name}</div>
                  <div className="j-showcase__filter-author">by {f.author}</div>
                </div>
                <div className="j-showcase__filter-hits">
                  <div className="j-showcase__filter-hits-value" style={{ color: tColor }}>{f.hits}</div>
                  <div className="j-showcase__filter-hits-label">seeds</div>
                </div>
              </div>
            )
          })}
        </div>

        {/* Recent finds header */}
        <div className="j-showcase__section-header">
          <div className="j-showcase__section-tag" style={{ background: C.GREEN }}>Recent Finds</div>
          <div className="j-showcase__section-rule" style={{ background: `${C.GREEN}55` }} />
        </div>

        {/* Recent finds list */}
        <div className="j-showcase__recent">
          {recentFinds.length === 0 ? (
            <div>No recent finds yet.</div>
          ) : recentFinds.map((r, i) => (
            <div key={i}>
              <span style={{ color: C.GOLD_TEXT }}>{r.seed}</span>
              {' · '}{r.filterName}
              {r.score > 0 && <span style={{ color: C.GREEN_TEXT }}> +{r.score}</span>}
            </div>
          ))}
        </div>

        <div style={{ height: 16 }} />
      </div>

      {/* Bottom actions */}
      <div className="j-showcase__actions">
        <JimboButton tone="green"   fullWidth size="md" onClick={onNewSearch}>New Search</JimboButton>
        <JimboButton tone="blue"    fullWidth size="md" onClick={onBrowseFilters}>Browse Filters</JimboButton>
        <JimboButton tone="orange"  fullWidth size="md" onClick={onBack}>Back</JimboButton>
      </div>
    </div>
  )
}
