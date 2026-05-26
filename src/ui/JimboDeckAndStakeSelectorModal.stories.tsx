import type { Meta, StoryObj } from '@storybook/react';
import { useState } from 'react';
import { JimboDeckAndStakeSelectorModal } from './JimboDeckAndStakeSelectorModal';
import { JimboApp } from './jimboApp';
import { JimboButton } from './panel';

const DECKS = [
  'Red', 'Blue', 'Yellow', 'Green', 'Black', 'Magic', 'Nebula', 'Ghost',
  'Abandoned', 'Checkered', 'Zodiac', 'Painted', 'Anaglyph', 'Plasma', 'Erratic',
];

const STAKES = ['White', 'Red', 'Green', 'Black', 'Blue', 'Purple', 'Orange', 'Gold'];

const meta = {
  title: 'JimboUI / JimboDeckAndStakeSelectorModal',
  component: JimboDeckAndStakeSelectorModal,
} satisfies Meta<typeof JimboDeckAndStakeSelectorModal>;

export default meta;
type Story = StoryObj<typeof meta>;

export const Default: Story = {
  render: () => {
    // eslint-disable-next-line react-hooks/rules-of-hooks
    const [open, setOpen] = useState(true);
    // eslint-disable-next-line react-hooks/rules-of-hooks
    const [deck, setDeck] = useState('Erratic');
    // eslint-disable-next-line react-hooks/rules-of-hooks
    const [stake, setStake] = useState('Gold');
    return (
      <JimboApp>
        <JimboButton onClick={() => setOpen(true)}>Open selector</JimboButton>
        <JimboDeckAndStakeSelectorModal
          open={open}
          onClose={() => setOpen(false)}
          decks={DECKS}
          stakes={STAKES}
          deck={deck}
          stake={stake}
          onDeckChange={setDeck}
          onStakeChange={setStake}
        />
      </JimboApp>
    );
  },
};
