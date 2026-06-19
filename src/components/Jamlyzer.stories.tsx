import type { Meta, StoryObj } from '@storybook/react-vite';
import { Jamlyzer } from './Jamlyzer';

const meta = {
  title: 'APPS / Jamlyzer',
  component: Jamlyzer,
} satisfies Meta<typeof Jamlyzer>;

export default meta;
type Story = StoryObj<typeof meta>;

export const Default: Story = {
  args: {
    jaml: `must:
  - joker: Blueprint
    antes: [1]
seeds:
  - FROGMANS
  - WEEJOKER
  - PERKEO99
`,
  },
};
