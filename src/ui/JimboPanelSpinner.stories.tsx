import type { Meta, StoryObj } from '@storybook/react';
import { useState } from 'react';
import { DeckSprite } from '../components/DeckSprite';
import { JimboPanelSpinner } from './JimboPanelSpinner';
import { JimboPanel } from './panel';
import { JimboText } from './jimboText';

// Vanilla deck list. Matches DECK_OPTIONS in src/lib/data/constants.ts plus
// the additional decks (Plasma, Ghost) included in DeckSprite's sprite map.
const DECKS = [
  'Red', 'Blue', 'Yellow', 'Green', 'Black', 'Magic', 'Nebula', 'Ghost',
  'Abandoned', 'Checkered', 'Zodiac', 'Painted', 'Anaglyph', 'Plasma', 'Erratic',
];

const STAKES = ['White', 'Red', 'Green', 'Black', 'Blue', 'Purple', 'Orange', 'Gold'];

const meta = {
  title: 'JimboUI / JimboPanelSpinner',
  component: JimboPanelSpinner,
  parameters: {
    jimboHarness: false,
    layout: 'centered',
  },
} satisfies Meta<typeof JimboPanelSpinner>;

export default meta;
type Story = StoryObj<typeof meta>;

export const DeckSelector: Story = {
  args: {
    label: 'Deck',
    title: 'Erratic Deck',
    description: 'All ranks and suits in deck are randomized',
    media: <DeckSprite deck="Erratic" size={64} />,
  },
};

/**
 * Combined Deck + Stake selector. This is the screen a seed hunter uses to
 * configure WHICH deck/stake combination they're searching seeds for — not
 * to start a game. Two stacked JimboPanelSpinners, each cycling through the
 * vanilla options; the stake sticker overlays on the deck sprite.
 */
export const DeckAndStakeSelector: StoryObj = {
  render: () => {
    // eslint-disable-next-line react-hooks/rules-of-hooks
    const [deckIdx, setDeckIdx] = useState(0);
    // eslint-disable-next-line react-hooks/rules-of-hooks
    const [stakeIdx, setStakeIdx] = useState(0);
    const deck = DECKS[deckIdx];
    const stake = STAKES[stakeIdx];

    return (
      <div style={{ width: 320 }}>
        <JimboPanel>
          <JimboText size="sm" tone="white" style={{ textAlign: 'center', marginBottom: 8 }}>
            Search for seeds matching:
          </JimboText>
          <JimboPanelSpinner
            label="Deck"
            title={`${deck} Deck`}
            description=" "
            media={<DeckSprite deck={deck} stake={stake} size={64} />}
            onPrev={() => setDeckIdx((p) => (p - 1 + DECKS.length) % DECKS.length)}
            onNext={() => setDeckIdx((p) => (p + 1) % DECKS.length)}
          />
          <JimboPanelSpinner
            label="Stake"
            title={`${stake} Stake`}
            description=" "
            media={<DeckSprite deck={deck} stake={stake} size={64} />}
            onPrev={() => setStakeIdx((p) => (p - 1 + STAKES.length) % STAKES.length)}
            onNext={() => setStakeIdx((p) => (p + 1) % STAKES.length)}
          />
        </JimboPanel>
      </div>
    );
  },
};
