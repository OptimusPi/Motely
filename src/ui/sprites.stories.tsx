import type { Meta, StoryObj } from '@storybook/react-vite';
import { DeckSprite, JimboSprite, StakeSprite } from './sprites';
import { JimboStack, JimboRow } from './jimboLayout';
import { JimboApp } from './jimboApp';

function Showcase() {
  return (
    <JimboApp>
      <JimboStack gap="xl" align="center">
        <JimboRow gap="lg" align="end">
          <JimboSprite name="Joker" sheet="Jokers" width={48} />
          <JimboSprite name="Blueprint" sheet="Jokers" width={48} />
          <JimboSprite name="The Fool" sheet="Tarots" width={48} />
        </JimboRow>
        <JimboRow gap="lg" align="center">
          <StakeSprite stake="White" width={32} />
          <StakeSprite stake="Gold" width={32} />
        </JimboRow>
        <JimboRow gap="lg" align="center">
          <DeckSprite deck="Red" width={56} />
          <DeckSprite deck="Erratic" width={56} />
        </JimboRow>
      </JimboStack>
    </JimboApp>
  );
}

const meta = {
  title: 'JimboUI / Sprites',
} satisfies Meta;

export default meta;
type Story = StoryObj<typeof meta>;

export const Default: Story = {
  render: () => <Showcase />,
};
