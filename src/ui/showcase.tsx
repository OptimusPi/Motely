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

const DEFAULT_STATS: ShowcaseLiveStats = { searched: '15.6B', matches: '2,847', speed: '5.4M/s' }

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
    <div style={{
      width: '100%', height: '100%', background: C.DARKEST,
      display: 'flex', flexDirection: 'column',
      fontFamily: 'm6x11plus, monospace', color: C.WHITE, overflow: 'hidden',
    }}>
      <div style={{ flex: 1, minHeight: 0, overflowY: 'auto', padding: '18px 14px 10px' }}>

        {/* Wordmark */}
        <div style={{ textAlign: 'center', marginBottom: 18 }}>
          <div style={{ fontSize: 32, letterSpacing: 3, lineHeight: 1, color: C.GOLD, textShadow: '2px 2px 0 rgba(0,0,0,.8)' }}>
            Balatro
          </div>
          <div style={{ fontSize: 14, letterSpacing: 4, color: C.GREY, marginTop: 4, textShadow: '1px 1px 0 rgba(0,0,0,.8)' }}>
            Seed · Curator
          </div>
        </div>

        {/* Live stats */}
        <div style={{
          background: C.DARK_GREY, borderRadius: 6, padding: 10,
          border: `2px solid ${C.PANEL_EDGE}`, boxShadow: `0 2px 0 ${C.BLACK}`,
          display: 'grid', gridTemplateColumns: '1fr 1fr 1fr', gap: 8, textAlign: 'center', marginBottom: 16,
        }}>
          {([
            [stats.searched, 'searched'],
            [stats.matches,  'matches'],
            [stats.speed,    'speed'],
          ] as const).map(([n, l]) => (
            <div key={l}>
              <div style={{ fontSize: 16, color: C.GOLD, textShadow: '1px 1px 0 rgba(0,0,0,.8)' }}>{n}</div>
              <div style={{ fontSize: 9, color: C.GREY, letterSpacing: 2, marginTop: 2 }}>{l}</div>
            </div>
          ))}
        </div>

        {/* Hot filters header */}
        <div style={{ display: 'flex', alignItems: 'center', gap: 8, marginBottom: 8 }}>
          <div style={{
            fontSize: 11, letterSpacing: 2, padding: '2px 8px',
            background: C.BLUE, color: C.WHITE, borderRadius: 3,
            textShadow: '1px 1px 0 rgba(0,0,0,.8)',
          }}>Hot Filters</div>
          <div style={{ flex: 1, height: 2, background: `${C.BLUE}55`, borderRadius: 1 }} />
        </div>

        {/* Filter cards */}
        <div style={{ display: 'flex', flexDirection: 'column', gap: 8, marginBottom: 16 }}>
          {hotFilters.map((f, i) => {
            const tColor = TONE_COLOR[f.tone]
            return (
              <div key={i} style={{
                background: C.DARK_GREY, borderRadius: 6, padding: 10,
                border: `2px solid ${tColor}`, boxShadow: `0 2px 0 ${C.BLACK}`,
                display: 'flex', alignItems: 'center', gap: 10, cursor: 'pointer',
              }}>
                <div style={{ display: 'flex', gap: 2 }}>
                  {f.sample.map((name, j) => (
                    <div key={j} style={{ width: 30, height: 40, display: 'flex', alignItems: 'center', justifyContent: 'center' }}>
                      <JimboSprite name={name} width={28} />
                    </div>
                  ))}
                </div>
                <div style={{ flex: 1, minWidth: 0 }}>
                  <div style={{
                    fontSize: 13, color: C.WHITE, letterSpacing: 1,
                    textShadow: '1px 1px 0 rgba(0,0,0,.8)',
                    overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap',
                  }}>{f.name}</div>
                  <div style={{ fontSize: 9, color: C.GOLD_TEXT, letterSpacing: 1, marginTop: 2 }}>by {f.author}</div>
                </div>
                <div style={{ textAlign: 'right' }}>
                  <div style={{ fontSize: 14, color: tColor, textShadow: '1px 1px 0 rgba(0,0,0,.8)' }}>{f.hits}</div>
                  <div style={{ fontSize: 8, color: C.GREY, letterSpacing: 1 }}>seeds</div>
                </div>
              </div>
            )
          })}
        </div>

        {/* Recent finds header */}
        <div style={{ display: 'flex', alignItems: 'center', gap: 8, marginBottom: 8 }}>
          <div style={{
            fontSize: 11, letterSpacing: 2, padding: '2px 8px',
            background: C.GREEN, color: C.WHITE, borderRadius: 3,
            textShadow: '1px 1px 0 rgba(0,0,0,.8)',
          }}>Recent Finds</div>
          <div style={{ flex: 1, height: 2, background: `${C.GREEN}55`, borderRadius: 1 }} />
        </div>

        {/* Recent finds list */}
        <div style={{
          background: C.DARK_GREY, borderRadius: 6, padding: '8px 10px',
          border: `2px solid ${C.PANEL_EDGE}`, boxShadow: `0 2px 0 ${C.BLACK}`,
          fontSize: 11, color: C.GREY, letterSpacing: 1, lineHeight: 1.7,
        }}>
          {recentFinds.length === 0 ? (
            <div style={{ color: C.GREY }}>No recent finds yet.</div>
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
      <div style={{
        padding: '8px 10px 10px', borderTop: `2px solid ${C.BLACK}`, background: C.DARK_GREY,
        display: 'flex', flexDirection: 'column', gap: 6,
      }}>
        <JimboButton tone="green"   fullWidth size="md" onClick={onNewSearch}>New Search</JimboButton>
        <JimboButton tone="blue"    fullWidth size="md" onClick={onBrowseFilters}>Browse Filters</JimboButton>
        <JimboButton tone="orange"  fullWidth size="md" onClick={onBack}>Back</JimboButton>
      </div>
    </div>
  )
}
