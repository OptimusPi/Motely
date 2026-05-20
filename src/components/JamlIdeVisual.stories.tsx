import type { Meta, StoryObj } from '@storybook/react';
import { useState } from 'react';
import { JamlIdeVisual } from './JamlIdeVisual';
import { jamlTextToVisualFilter } from '../utils/jamlVisualFilter.js';
import sampleJaml from './fixtures/ide-sample.jaml?raw';

const parsedFilter = jamlTextToVisualFilter(sampleJaml);

function Demo() {
  const [filter, setFilter] = useState(parsedFilter);
  return (
    <div style={{ flex: 1, minHeight: 0, display: 'flex', flexDirection: 'column' }}>
      <JamlIdeVisual filter={filter} onChange={setFilter} />
    </div>
  );
}

const meta = {
  title: 'JAML / JamlIdeVisual',
  component: JamlIdeVisual,
  parameters: {
    jimboHarness: true,
    layout: 'fullscreen',
  },
} satisfies Meta<typeof JamlIdeVisual>;

export default meta;
type Story = StoryObj<typeof meta>;

export const Default: Story = {
  render: () => <Demo />,
};
