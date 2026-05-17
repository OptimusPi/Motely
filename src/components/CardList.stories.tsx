import type { Meta, StoryObj } from '@storybook/react';
import { CardFan } from './CardFan';
import { CardList } from './CardList';

const meta = {
  title: 'JAML / CardList',
  component: CardList,
  parameters: {
    jimboHarness: false,
    layout: 'centered',
  },
  decorators: [
    (Story) => <div style={{ width: 320, padding: 16 }}><Story /></div>,
  ],
} satisfies Meta<typeof CardList>;

export default meta;
type Story = StoryObj<typeof meta>;

export const Default: Story = {
  render: () => (
    <CardList title="Shop picks" subtitle="Ante 1">
      <CardFan cards={['A_S', 'K_S', 'Q_S']} />
      <CardFan cards={['2_C', '3_C', '4_C']} />
    </CardList>
  ),
};
