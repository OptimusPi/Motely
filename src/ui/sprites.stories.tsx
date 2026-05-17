import type { Meta, StoryObj } from '@storybook/react';
import { DeckSprite, JimboSprite, StakeSprite } from './sprites';

function Showcase() {
  return (
    <div style={{ width: 320, display: 'flex', flexDirection: 'column', gap: 16, alignItems: 'center' }}>
      <div style={{ display: 'flex', gap: 12, alignItems: 'flex-end' }}>
        <JimboSprite name="Joker" sheet="Jokers" width={48} />
        <JimboSprite name="Blueprint" sheet="Jokers" width={48} />
        <JimboSprite name="The Fool" sheet="Tarots" width={48} />
      </div>
      <div style={{ display: 'flex', gap: 12, alignItems: 'center' }}>
        <StakeSprite stake="White" width={32} />
        <StakeSprite stake="Gold" width={32} />
      </div>
      <div style={{ display: 'flex', gap: 12, alignItems: 'center' }}>
        <DeckSprite deck="Red" width={56} />
        <DeckSprite deck="Erratic" width={56} />
      </div>
    </div>
  );
}

const meta = {
  title: 'JimboUI / Sprites',
  parameters: {
    jimboHarness: false,
    layout: 'centered',
  },
} satisfies Meta;

export default meta;
type Story = StoryObj<typeof meta>;

export const Default: Story = {
  render: () => <Showcase />,
};
