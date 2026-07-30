import type { Meta, StoryObj } from '@storybook/react-vite';
import { JamlIde } from './JamlIde';
import { JimboApp } from '../ui/JimboApp.js';

const meta = {
  title: 'Screens/JAML IDE/IDE Shell',
  component: JamlIde,
  parameters: { layout: 'fullscreen' },
  decorators: [
    (Story) => (
      <JimboApp>
        <Story />
      </JimboApp>
    ),
  ],
} satisfies Meta<typeof JamlIde>;

export default meta;
type Story = StoryObj<typeof meta>;

export const Default: Story = {
  args: {
    defaultJaml: `must:
  - joker: Blueprint
    antes: [1]
should:
  - voucher: Telescope
    antes: [1]
`,
  },
};

export const WithResults: Story = {
  args: {
    defaultJaml: `must:
  - joker: Blueprint
    antes: [1]
`,
    searchResults: [
      { seed: 'WEEJOKER', score: 100, tallyColumns: [100], tallyLabels: ['Must match'] },
      { seed: 'PERKEO99', score: 80, tallyColumns: [80], tallyLabels: ['Must match'] },
    ],
  },
};
