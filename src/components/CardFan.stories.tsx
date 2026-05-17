import type { Meta, StoryObj } from '@storybook/react';
import { CardFan } from './CardFan';

const meta = {
  title: 'JAML / CardFan',
  component: CardFan,
  parameters: {
    jimboHarness: false,
    layout: 'centered',
  },
  decorators: [
    (Story) => <div style={{ width: 320, padding: 16 }}><Story /></div>,
  ],
} satisfies Meta<typeof CardFan>;

export default meta;
type Story = StoryObj<typeof meta>;

export const Hand: Story = {
  args: {
    cards: ['A_S', '10_H', '6_D', '6_C', 'J_S'],
    label: 'Opening hand',
  },
};

export const FullDeck: Story = {
  args: {
    count: 52,
    label: 'Packed deck',
  },
};
