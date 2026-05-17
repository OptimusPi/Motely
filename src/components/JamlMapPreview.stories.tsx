import type { Meta, StoryObj } from '@storybook/react';
import { JamlMapPreview } from './JamlMapPreview';

const jaml = `must:\n  - joker: Blueprint\n  - voucher: Crystal Ball\nshould:\n  - uncommonJoker: Oops! All 6s\nmustNot:\n  - boss: The Needle\n`;

const meta = {
  title: 'JAML / JamlMapPreview',
  component: JamlMapPreview,
  parameters: {
    jimboHarness: false,
    layout: 'centered',
  },
  decorators: [
    (Story) => <div style={{ width: 320 }}><Story /></div>,
  ],
} satisfies Meta<typeof JamlMapPreview>;

export default meta;
type Story = StoryObj<typeof meta>;

export const Default: Story = {
  args: {
    jaml,
  },
};

export const WithTallies: Story = {
  args: {
    jaml,
    tallyLabels: ['must: joker: Blueprint', 'must: voucher: Crystal Ball', 'should: uncommonJoker: Oops! All 6s'],
    tallyColumns: [1, 1, 2],
  },
};
