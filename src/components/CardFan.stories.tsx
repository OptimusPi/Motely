import type { Meta, StoryObj } from '@storybook/react';
import { CardFan } from './CardFan';
import { JimboPanel } from '../ui/panel';
import { JimboSectionHeader } from '../ui/jimboSectionHeader';

const meta = {
  title: 'JAML / CardFan',
  component: CardFan,
} satisfies Meta<typeof CardFan>;

export default meta;
type Story = StoryObj<typeof meta>;

export const Hand: Story = {
  render: () => (
    <JimboPanel>
      <JimboSectionHeader label="Opening hand" tone="blue" />
      <CardFan cards={['A_S', '10_H', '6_D', '6_C', 'J_S']} />
    </JimboPanel>
  ),
};

export const WideHand: Story = {
  render: () => (
    <JimboPanel>
      <JimboSectionHeader label="8-card hand" tone="gold" />
      <CardFan cards={['A_S', 'K_H', 'Q_D', 'J_C', '10_S', '9_H', '8_D', '7_C']} />
    </JimboPanel>
  ),
};

export const Empty: Story = {
  render: () => (
    <JimboPanel>
      <JimboSectionHeader label="Empty hand" tone="red" />
      <CardFan cards={[]} />
    </JimboPanel>
  ),
};
