import type { Meta, StoryObj } from '@storybook/react-vite';
import { MotelyHello } from './MotelyHello';

const meta = {
  title: 'MOTELY / Hello',
  component: MotelyHello,
} satisfies Meta<typeof MotelyHello>;

export default meta;
type Story = StoryObj<typeof meta>;

export const Default: Story = {};

export const QuickSearch: Story = {
  args: { searchCount: 500 },
};

export const BigSearch: Story = {
  args: { searchCount: 50000 },
};
