import type { Meta, StoryObj } from '@storybook/react';
import { JimboCodeBlock } from './codeBlock';

const meta = {
  title: 'JimboUI / JimboCodeBlock',
  component: JimboCodeBlock,
} satisfies Meta<typeof JimboCodeBlock>;

export default meta;
type Story = StoryObj<typeof meta>;

export const Default: Story = {
  args: {
    filename: 'route.jaml',
    language: 'JAML',
    code: 'must:\n  - joker: Blueprint\nshould:\n  - uncommonJoker: Oops! All 6s\n',
  },
};
