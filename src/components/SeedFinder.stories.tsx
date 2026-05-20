import type { Meta, StoryObj } from '@storybook/react'
import { useState } from 'react'
import { useSearch } from '../hooks/useSearch.js'
import { JimboPanel, JimboInnerPanel, JimboButton } from '../ui/panel.js'
import { JimboText } from '../ui/jimboText.js'
import { JimboBadge } from '../ui/JimboBadge.js'
import { JimboAppScroll, JimboAppFooter } from '../ui/jimboApp.js'
import { JimboSectionHeader } from '../ui/jimboSectionHeader.js'
import { JimboCopyButton } from '../ui/JimboCopyButton.js'

interface Archetype {
  slug: string
  label: string
  jaml: string
}

const ARCHETYPES: Archetype[] = [
  {
    slug: 'blueprint',
    label: 'Blueprint A1',
    jaml: JSON.stringify({ deck: 'Red', stake: 'White', must: [{ joker: 'Blueprint', antes: [1] }] }),
  },
  {
    slug: 'brainstorm',
    label: 'Brainstorm A1',
    jaml: JSON.stringify({ deck: 'Red', stake: 'White', must: [{ joker: 'Brainstorm', antes: [1] }] }),
  },
  {
    slug: 'perkeo',
    label: 'Early Perkeo',
    jaml: JSON.stringify({ deck: 'Red', stake: 'White', should: [{ legendaryJoker: 'Perkeo', antes: [1, 2, 3], score: 40 }] }),
  },
  {
    slug: 'matador',
    label: 'Matador A1',
    jaml: JSON.stringify({ deck: 'Red', stake: 'White', must: [{ joker: 'Matador', antes: [1] }] }),
  },
]

const SEED_COUNT = 50_000

function SeedFinder() {
  const [picked, setPicked] = useState<Archetype>(ARCHETYPES[0])
  const { results, status, totalSearched, seedsPerSecond, error, startRandom, cancel, reset } = useSearch()
  const running = status === 'running'

  return (
    <>
      <JimboAppScroll>
        <JimboPanel>
          <JimboSectionHeader label="Archetype" tone="blue" />
          <div className="j-flex j-flex-wrap j-gap-sm">
            {ARCHETYPES.map((a) => (
              <JimboButton
                key={a.slug}
                size="xs"
                tone={a.slug === picked.slug ? 'orange' : 'grey'}
                disabled={running}
                onClick={() => { setPicked(a); reset() }}
              >
                {a.label}
              </JimboButton>
            ))}
          </div>

          <JimboSectionHeader label="Results" tone="gold" />

          <div className="j-flex j-items-center j-justify-between">
            <JimboBadge tone={running ? 'orange' : status === 'completed' ? 'green' : 'grey'}>
              {status}
            </JimboBadge>
            <JimboText size="xs" tone="white">
              {totalSearched.toLocaleString()} searched
            </JimboText>
            {seedsPerSecond > 0 && (
              <JimboText size="xs" tone="gold">
                {Math.round(seedsPerSecond / 1000)}k/s
              </JimboText>
            )}
          </div>

          {error && (
            <JimboText size="xs" tone="red" className="j-text-center">{error}</JimboText>
          )}

          <JimboInnerPanel>
            {results.length === 0 ? (
              <JimboText size="xs" tone="white" className="j-text-center">
                {running ? 'Searching...' : 'Pick an archetype and search.'}
              </JimboText>
            ) : (
              results.map((r) => (
                <div key={r.seed} className="j-flex j-items-center j-justify-between">
                  <JimboText size="sm" tone="gold" style={{ letterSpacing: 1 }}>{r.seed}</JimboText>
                  <div className="j-flex j-items-center j-gap-sm">
                    <JimboBadge tone="orange">{r.score}</JimboBadge>
                    <JimboCopyButton value={r.seed} size="xs" />
                  </div>
                </div>
              ))
            )}
          </JimboInnerPanel>
        </JimboPanel>
      </JimboAppScroll>

      <JimboAppFooter>
        {running ? (
          <JimboButton tone="red" size="md" fullWidth onClick={cancel}>Cancel</JimboButton>
        ) : (
          <JimboButton tone="green" size="md" fullWidth onClick={() => startRandom(picked.jaml, SEED_COUNT)}>
            Search {SEED_COUNT.toLocaleString()} seeds
          </JimboButton>
        )}
      </JimboAppFooter>
    </>
  )
}

const meta = {
  title: 'Apps / SeedFinder',
  component: SeedFinder,
  parameters: { jimboHarness: true, layout: 'fullscreen' },
} satisfies Meta<typeof SeedFinder>

export default meta
type Story = StoryObj<typeof meta>

export const Default: Story = {}
