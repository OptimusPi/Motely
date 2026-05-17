import type { Meta, StoryObj } from '@storybook/react';
import { JamlGameCard } from './GameCard';

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

