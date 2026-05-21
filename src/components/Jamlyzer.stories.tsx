import type { Meta, StoryObj } from '@storybook/react';
import { Jamlyzer } from './Jamlyzer';
import sampleJaml from './fixtures/jamlyzer-sample.jaml?raw';
import swipeJaml from './fixtures/jamlyzer-swipe.jaml?raw';

const meta = {
  title: 'JAML / Jamlyzer',
  component: Jamlyzer,
  parameters: {
    jimboHarness: true,
    layout: 'fullscreen',
  },
} satisfies Meta<typeof Jamlyzer>;

export default meta;
type Story = StoryObj<typeof meta>;

/** Minimal fixture — three seeds, one must clause. Shows timing + swipe. */
export const Default: Story = {
  render: () => (
    <div style={{ flex: 1, minHeight: 0, display: 'flex', flexDirection: 'column', padding: 8 }}>
      <Jamlyzer jaml={sampleJaml} style={{ flex: 1, minHeight: 0 }} />
    </div>
  ),
};

/**
 * Same filter as ide-sample. Populate `seeds:` with Motely CLI --save-seeds
 * (see comments at top of jamlyzer-swipe.jaml).
 */
export const SwipeBench: Story = {
  render: () => (
    <div style={{ flex: 1, minHeight: 0, display: 'flex', flexDirection: 'column', padding: 8 }}>
      <Jamlyzer jaml={swipeJaml} style={{ flex: 1, minHeight: 0 }} />
    </div>
  ),
};
