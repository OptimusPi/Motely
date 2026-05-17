import type { Meta, StoryObj } from '@storybook/react';
import { JimboText } from '../ui/jimboText';
import { JamlGameCard } from './GameCard';

function CardShowcase() {
  const cards = [
    { label: 'Common', name: 'Joker' },
    { label: 'Uncommon', name: 'Oops! All 6s' },
    { label: 'Rare', name: 'Blueprint' },
    { label: 'Legendary', name: 'Perkeo' },
  ] as const;

  return (
    <div className="j-flex j-gap-lg j-flex-wrap" style={{ justifyContent: 'center' }}>
      {cards.map((card) => (
        <div key={card.name} className="j-flex j-flex-col j-items-center j-gap-sm">
          <JamlGameCard
            type="joker"
            card={{ name: card.name }}
            hoverTilt
          />
          <div className="j-text-center">
            <JimboText size="xs" tone="grey">{card.label}</JimboText>
            <JimboText size="sm" tone="white">{card.name}</JimboText>
          </div>
        </div>
      ))}
    </div>
  );
}

const meta = {
  title: 'JAML / JamlGameCard',
  component: JamlGameCard,
  parameters: { jimboHarness: false, 
    layout: 'centered',
  },
} satisfies Meta<typeof JamlGameCard>;

export default meta;
type Story = StoryObj<typeof meta>;

export const DefaultJoker: Story = {
  args: {
    type: 'joker',
    card: {
      name: 'Joker',
    },
    hoverTilt: true,
  },
};

export const Showcase: Story = {
  render: () => <CardShowcase />,
};

export const FoilJoker: Story = {
  args: {
    type: 'joker',
    card: {
      name: 'Joker',
      edition: 'Foil',
    },
    hoverTilt: true,
  },
};

export const EternalJoker: Story = {
  args: {
    type: 'joker',
    card: {
      name: 'Joker',
      isEternal: true,
    },
    hoverTilt: true,
  },
};

export const PlayingCard: Story = {
  args: {
    type: 'playing',
    card: {
      name: 'Ace of Spades',
      rank: 'Ace',
      suit: 'Spades',
      edition: 'Polychrome',
    },
    hoverTilt: true,
  },
};

