import type { Meta, StoryObj } from '@storybook/react';
import React, { useState } from 'react';
import { JamlIdeVisual, type JamlVisualFilter } from './JamlIdeVisual';

const initialFilter: JamlVisualFilter = {
  name: 'Seed route',
  author: 'pifreak',
  description: 'Open with Blueprint, avoid dead antes.',
  deck: 'Erratic',
  stake: 'White',
  must: [
    { id: 'm1', type: 'joker', value: 'Blueprint', label: 'Blueprint', antes: [1] },
    { id: 'm2', type: 'voucher', value: 'Crystal Ball', label: 'Crystal Ball' },
  ],
  should: [
    { id: 's1', type: 'uncommonJoker', value: 'Oops! All 6s', label: 'Oops! All 6s', antes: [1, 2], score: 3 },
  ],
  mustnot: [
    { id: 'n1', type: 'boss', value: 'The Needle', label: 'The Needle' },
  ],
};

function Demo() {
  const [filter, setFilter] = useState(initialFilter);
  return <JamlIdeVisual filter={filter} onChange={setFilter} />;
}

const meta = {
  title: 'JAML / JamlIdeVisual',
  component: JamlIdeVisual,
  parameters: {
    jimboHarness: false,
    layout: 'centered',
  },
  decorators: [
    (Story) => <div style={{ width: 320 }}><Story /></div>,
  ],
} satisfies Meta<typeof JamlIdeVisual>;

export default meta;
type Story = StoryObj<typeof meta>;

export const Default: Story = {
  render: () => <Demo />,
};
