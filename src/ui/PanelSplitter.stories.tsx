import type { Meta, StoryObj } from '@storybook/react';
import { useState } from 'react';
import { PanelSplitter } from './PanelSplitter';
import { JimboPanel } from './panel';
import { JimboText } from './jimboText';
import { JimboCodeBlock } from './codeBlock';

function Demo() {
  const [left, setLeft] = useState(140);
  const adjust = (delta: number) => setLeft((value) => Math.max(108, Math.min(204, value + delta)));

  return (
    <div className="j-story-phone j-panel-splitter-story">
      <JimboPanel className="j-panel-splitter-story__shell">
        <div className="j-panel-splitter-story__header">
          <JimboText size="md" tone="gold">JAML split view</JimboText>
          <JimboText size="xs" tone="grey">Drag or use arrow keys</JimboText>
        </div>
        <div className="j-panel-splitter-story__body">
          <section className="j-panel-splitter-story__pane" style={{ flexBasis: left }}>
            <JimboText size="xs" tone="white" className="j-panel-splitter-story__label">route.jaml</JimboText>
            <JimboCodeBlock
              filename="route.jaml"
              language="JAML"
              code={'must:\n  - joker: Blueprint\n  - legendaryJoker: Perkeo\nshould:\n  - voucher: Observatory\n'}
            />
          </section>
          <PanelSplitter orientation="vertical" onDrag={adjust} onKeyAdjust={adjust} />
          <section className="j-panel-splitter-story__pane j-panel-splitter-story__pane--results">
            <JimboText size="xs" tone="white" className="j-panel-splitter-story__label">Preview</JimboText>
            <div className="j-panel-splitter-story__result-row"><JimboText size="xs" tone="gold">Blueprint</JimboText><JimboText size="xs" tone="green">must</JimboText></div>
            <div className="j-panel-splitter-story__result-row"><JimboText size="xs" tone="gold">Perkeo</JimboText><JimboText size="xs" tone="green">must</JimboText></div>
            <div className="j-panel-splitter-story__result-row"><JimboText size="xs" tone="gold">Observatory</JimboText><JimboText size="xs" tone="blue">should</JimboText></div>
          </section>
        </div>
      </JimboPanel>
    </div>
  );
}

const meta = {
  title: 'JimboUI / PanelSplitter',
  component: PanelSplitter,
  parameters: {
    jimboHarness: false,
    layout: 'centered',
  },
} satisfies Meta<typeof PanelSplitter>;

export default meta;
type Story = StoryObj<typeof meta>;

export const Vertical: Story = {
  render: () => <Demo />,
};
