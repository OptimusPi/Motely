import type { Meta, StoryObj } from '@storybook/react-vite';
import { JamlCurator } from './JamlCurator';

const meta = {
  title: 'APPS / JAML Curator',
  component: JamlCurator,
} satisfies Meta<typeof JamlCurator>;

export default meta;
type Story = StoryObj<typeof meta>;

export const Default: Story = {};
