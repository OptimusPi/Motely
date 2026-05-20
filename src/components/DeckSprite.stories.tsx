import type { Meta, StoryObj } from '@storybook/react';
import { DeckSprite } from './DeckSprite';

const meta = {
  title: 'JAML / DeckSprite',
  component: DeckSprite,
} satisfies Meta<typeof DeckSprite>;

export default meta;
type Story = StoryObj<typeof meta>;

export const Default: Story = {
  args: {
    deck: 'red',
    size: 100,
  },
};

export const WithStake: Story = {
  args: {
    deck: 'blue',
    stake: 'gold',
    size: 100,
  },
};

export const AllDecks: Story = {
  render: () => (
    <div style={{ display: 'flex', gap: 10, flexWrap: 'wrap', width: 400 }}>
      {['red', 'blue', 'yellow', 'green', 'black', 'magic', 'ghost', 'plasma', 'erratic', 'abandoned', 'checkered', 'painted', 'anaglyph'].map(deck => (
        <DeckSprite key={deck} deck={deck} size={50} />
      ))}
    </div>
  ),
};

