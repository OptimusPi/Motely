import type { Meta, StoryObj } from '@storybook/react';
import { JamlCurator } from './JamlCurator';

const meta = {
  title: 'JAML / JamlCurator',
  component: JamlCurator,
  parameters: {
    jimboHarness: false,
    layout: 'centered',
  },
} satisfies Meta<typeof JamlCurator>;

export default meta;
type Story = StoryObj<typeof meta>;

export const Default: Story = {};
