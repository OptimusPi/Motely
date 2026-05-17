import type { Meta, StoryObj } from '@storybook/react';
import { JimboButton } from './panel';
import { JimboText } from './jimboText';
import { JimboTooltip } from './jimboTooltip';

const meta = {
  title: 'JimboUI / JimboTooltip',
  component: JimboTooltip,
  parameters: {
    jimboHarness: false,
    layout: 'centered',
  },
  decorators: [
    (Story) => <div style={{ width: 320, height: 200, display: 'flex', alignItems: 'center', justifyContent: 'center' }}><Story /></div>,
  ],
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
