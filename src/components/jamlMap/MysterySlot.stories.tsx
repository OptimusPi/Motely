import type { Meta, StoryObj } from '@storybook/react';
import { MysterySlot } from './MysterySlot';

const meta = {
  title: 'JAML/MysterySlot',
  component: MysterySlot,
  parameters: {
    layout: 'centered',
  },
} satisfies Meta<typeof MysterySlot>;

export default meta;
type Story = StoryObj<typeof meta>;

export const Empty: Story = {
  args: {
    zone: 'must',
    width: 64,
  },
  render: (args) => (
    <div style={{ background: '#1e2b2d', padding: 20 }}>
      <MysterySlot {...args} />
    </div>
  ),
};

export const FilledJoker: Story = {
  args: {
    zone: 'must',
    width: 64,
    selection: {
      clauseKey: 'joker',
      value: 'Joker',
      displayLabel: 'Joker',
      spriteName: 'Joker',
      sheetType: 'Jokers',
    },
  },
  render: (args) => (
    <div style={{ background: '#1e2b2d', padding: 20 }}>
      <MysterySlot {...args} />
    </div>
  ),
};
