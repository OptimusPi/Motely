import type { Meta, StoryObj } from '@storybook/react';
import { MotelyVersionBadge } from './MotelyVersionBadge';

const meta = {
  title: 'JAML / MotelyVersionBadge',
  component: MotelyVersionBadge,
  parameters: {
    jimboHarness: false,
    layout: 'centered',
  },
} satisfies Meta<typeof MotelyVersionBadge>;

export default meta;
type Story = StoryObj<typeof meta>;

export const Full: Story = {
  args: {
    caps: { version: '17.7.0', simd: true, threads: true },
  },
};

export const Minimal: Story = {
  args: {
    caps: { version: '17.7.0', simd: true, threads: false },
    minimal: true,
  },
};
