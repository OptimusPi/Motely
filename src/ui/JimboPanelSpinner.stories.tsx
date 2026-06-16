<<<<<<< HEAD
import type { Meta, StoryObj } from '@storybook/react';
=======
import type { Meta, StoryObj } from '@storybook/react-vite';
>>>>>>> 4c1c0b639ac307d7366dccd1170ebadffbc2ab45
import { useState } from 'react';
import { DeckSprite } from '../components/DeckSprite';
import { JimboPanelSpinner } from './JimboPanelSpinner';
import { JimboPanel } from './panel';
import { JimboText } from './jimboText';
import { JimboApp } from './jimboApp';
import { JimboStack } from './jimboLayout';

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
 * Combined Deck + Stake selector. The screen a seed hunter uses to configure
 * WHICH deck/stake to search seeds for — not to start a game.
 */
export const DeckAndStakeSelector: StoryObj = {
  render: () => {
<<<<<<< HEAD
    // eslint-disable-next-line react-hooks/rules-of-hooks
    const [deckIdx, setDeckIdx] = useState(0);
    // eslint-disable-next-line react-hooks/rules-of-hooks
=======
     
    const [deckIdx, setDeckIdx] = useState(0);
     
>>>>>>> 4c1c0b639ac307d7366dccd1170ebadffbc2ab45
    const [stakeIdx, setStakeIdx] = useState(0);
    const deck = DECKS[deckIdx];
    const stake = STAKES[stakeIdx];

    return (
      <JimboApp>
        <JimboPanel>
          <JimboStack gap="md" align="stretch">
            <JimboStack align="center">
              <JimboText size="sm" tone="white">Search for seeds matching:</JimboText>
            </JimboStack>
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
          </JimboStack>
        </JimboPanel>
      </JimboApp>
    );
  },
};
