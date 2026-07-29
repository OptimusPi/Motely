import type { Meta, StoryObj } from '@storybook/react-vite';
import { JimboMascot } from './JimboMascot';
import { JimboRow } from './JimboLayout';

const meta = {
  title: 'Jimbo/Mascot/Jammy',
  component: JimboMascot,
} satisfies Meta<typeof JimboMascot>;
export default meta;

export const Moods: StoryObj<typeof meta> = {
  render: () => (
    <JimboRow gap="xl" align="center">
      <JimboMascot mood="idle" />
      <JimboMascot mood="happy" />
      <JimboMascot mood="surprised" />
    </JimboRow>
  ),
};

export const RadialMenu: StoryObj<typeof meta> = {
  render: () => (
    <JimboRow gap="xl" justify="center" className="j-story-stage">
      <JimboMascot
        menuItems={[
          { label: 'Search', action: 'search', tone: 'blue' },
          { label: 'Analyze', action: 'analyze', tone: 'green' },
          { label: 'Copy', action: 'copy', tone: 'purple' },
          { label: 'Help', action: 'help', tone: 'grey' },
        ]}
        onMenuAction={(action) => console.log('menu action', action)}
      />
    </JimboRow>
  ),
};
