'use client'

import React from 'react'
import { JimboModal } from './panel.js'
import { JimboPanelSpinner } from './JimboPanelSpinner.js'
import { JimboStack } from './jimboLayout.js'
import { DeckSprite, StakeSprite } from './sprites.js'

export interface JimboDeckAndStakeSelectorModalProps {
  /** Whether the modal is open. */
  open: boolean
  /** Called when the user dismisses via the back button. */
  onClose: () => void
  /** All decks the user can cycle through, in spinner order. */
  decks: string[]
  /** All stakes the user can cycle through, in spinner order. */
  stakes: string[]
  /** Currently selected deck name (must appear in `decks`). */
  deck: string
  /** Currently selected stake name (must appear in `stakes`). */
  stake: string
  onDeckChange: (next: string) => void
  onStakeChange: (next: string) => void
  /** Modal title text. Default: "Search seeds for". */
  title?: string
  /** Description rendered under each deck / stake title. */
  deckDescription?: (deck: string) => React.ReactNode
  stakeDescription?: (stake: string) => React.ReactNode
}

function cycle(list: string[], current: string, direction: -1 | 1): string {
  const idx = list.indexOf(current)
  if (idx < 0) return list[0] ?? current
  const next = (idx + direction + list.length) % list.length
  return list[next] ?? current
}

/**
 * Modal that lets a seed hunter pick the deck + stake combination they want
 * to search seeds against. Pure JimboUI composition — `JimboModal`, two
 * `JimboPanelSpinner`s, deck + stake sprites. No motely-wasm dependency; the
 * caller passes deck/stake names as strings (convert from `MotelyDeck` /
 * `MotelyStake` enums at the boundary).
 */
export function JimboDeckAndStakeSelectorModal({
  open,
  onClose,
  decks,
  stakes,
  deck,
  stake,
  onDeckChange,
  onStakeChange,
  title = 'Search seeds for',
  deckDescription,
  stakeDescription,
}: JimboDeckAndStakeSelectorModalProps) {
  return (
    <JimboModal open={open} onClose={onClose} title={title}>
      <JimboStack gap="md" align="stretch">
        <JimboPanelSpinner
          label="Deck"
          title={`${deck} Deck`}
          description={deckDescription ? deckDescription(deck) : ' '}
          media={<DeckSprite deck={deck} width={64} />}
          onPrev={() => onDeckChange(cycle(decks, deck, -1))}
          onNext={() => onDeckChange(cycle(decks, deck, 1))}
        />
        <JimboPanelSpinner
          label="Stake"
          title={`${stake} Stake`}
          description={stakeDescription ? stakeDescription(stake) : ' '}
          media={<StakeSprite stake={stake} width={48} />}
          onPrev={() => onStakeChange(cycle(stakes, stake, -1))}
          onNext={() => onStakeChange(cycle(stakes, stake, 1))}
        />
      </JimboStack>
    </JimboModal>
  )
}
