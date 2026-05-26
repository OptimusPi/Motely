import type { Meta, StoryObj } from '@storybook/react';
import { JimboButton } from './panel';
import { JimboText } from './jimboText';
import { JimboTooltip } from './jimboTooltip';

const meta = {
  title: 'JimboUI / JimboTooltip',
  component: JimboTooltip,
} satisfies Meta<typeof JimboTooltip>;

export default meta;
type Story = StoryObj<typeof meta>;

export const Default: Story = {
  render: () => (
    <JimboTooltip content={<JimboText size="sm" tone="white">Copies the Joker to the right.</JimboText>}>
      <JimboButton tone="red" size="sm">Hover</JimboButton>
    </JimboTooltip>
  ),
};
