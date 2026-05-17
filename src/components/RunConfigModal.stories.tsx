import type { Meta, StoryObj } from '@storybook/react';
import React, { useState } from 'react';
import { RunConfigModal } from './RunConfigModal';

function Demo() {
  const [open, setOpen] = useState(true);
  const [deck, setDeck] = useState('Red');
  const [stake, setStake] = useState('White');

  return (
    <RunConfigModal
      open={open}
      onClose={() => setOpen(false)}
      deck={deck}
      stake={stake}
      onChange={(nextDeck, nextStake) => {
        setDeck(nextDeck);
        setStake(nextStake);
      }}
    />
  );
}

const meta = {
  title: 'JAML / RunConfigModal',
  component: RunConfigModal,
  parameters: {
    jimboHarness: true,
    layout: 'fullscreen',
  },
} satisfies Meta<typeof RunConfigModal>;

export default meta;
type Story = StoryObj<typeof meta>;

export const Default: Story = {
  render: () => <Demo />,
};
