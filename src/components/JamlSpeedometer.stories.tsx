import type { Meta, StoryObj } from '@storybook/react';
import { JamlSpeedometer } from './JamlSpeedometer';

const meta = {
  title: 'JAML / JamlSpeedometer',
  component: JamlSpeedometer,
} satisfies Meta<typeof JamlSpeedometer>;

export default meta;
type Story = StoryObj<typeof meta>;

export const Idle: Story = {
  args: {
    seedsPerSecond: 0,
    totalSearched: 0,
    matchingSeeds: 0,
    status: 'idle',
  },
};

export const Running: Story = {
  args: {
    seedsPerSecond: 1250000,
    totalSearched: 8540000,
    matchingSeeds: 142,
    status: 'running',
  },
};

export const Completed: Story = {
  args: {
    seedsPerSecond: 0,
    totalSearched: 10000000,
    matchingSeeds: 156,
    status: 'completed',
  },
};

