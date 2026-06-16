<<<<<<< HEAD
import type { Meta, StoryObj } from '@storybook/react';
=======
import type { Meta, StoryObj } from '@storybook/react-vite';
>>>>>>> 4c1c0b639ac307d7366dccd1170ebadffbc2ab45
import { JimboStatGrid } from './jimboStatGrid';

const meta = {
  title: 'JimboUI / JimboStatGrid',
  component: JimboStatGrid,
} satisfies Meta<typeof JimboStatGrid>;

export default meta;
type Story = StoryObj<typeof meta>;

export const Default: Story = {
  args: {
    items: [
      { value: 42, label: 'jokers' },
      { value: '1.2M', label: 'seeds' },
      { value: 7, label: 'tags' },
    ],
  },
};

export const LongValues: Story = {
  args: {
    items: [
      { value: '999,999', label: 'attempts' },
      { value: '∞', label: 'remaining' },
      { value: '0.001s', label: 'fastest' },
    ],
  },
};
