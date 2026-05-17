import type { Meta, StoryObj } from '@storybook/react';
import { JimboBadge } from './JimboBadge';
import { JimboFloating } from './JimboFloating';

const meta = {
  title: 'JimboUI / JimboFloating',
  component: JimboFloating,
  parameters: {
    jimboHarness: false,
    layout: 'centered',
  },
  decorators: [
    (Story) => <div style={{ width: 320, height: 220, position: 'relative', background: '#1e2b2d', borderRadius: 8 }}><Story /></div>,
  ],
} satisfies Meta<typeof JimboFloating>;

export default meta;
type Story = StoryObj<typeof meta>;

export const TopRight: Story = {
  args: {
    anchor: 'top-right',
    children: <JimboBadge tone="red">Pinned</JimboBadge>,
  },
};
