import type { Meta, StoryObj } from '@storybook/react';
import { JamlSeedInput } from './JamlSeedInput';

const meta = {
  title: 'JAML / JamlSeedInput',
  component: JamlSeedInput,
  parameters: { jimboHarness: false, 
    layout: 'centered',
  },
  argTypes: {
    onSeedValid: { action: 'onSeedValid' },
  },
} satisfies Meta<typeof JamlSeedInput>;

export default meta;
type Story = StoryObj<typeof meta>;

export const Default: Story = {
  args: {
    initialSeed: '',
  },
};

export const PreFilled: Story = {
  args: {
    initialSeed: '1A2B3C4D',
  },
};

