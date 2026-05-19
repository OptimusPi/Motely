import type { Meta, StoryObj } from '@storybook/react';
import { useState } from 'react';
import { JimboPanelSplitter } from './JimboPanelSplitter';
import { JimboPanel } from './panel';
import { JimboText } from './jimboText';
import { JimboCodeBlock } from './codeBlock';
import { JimboInfoCard, JimboInfoCardBody, JimboInfoCardTitle, JimboInfoCardAside } from './jimboInfoCard';
import { JimboBadge } from './JimboBadge';

// The splitter slides ALL the way to either edge — left can fully collapse to
// 0 (right pane takes the entire width), and right can fully collapse too. No
// lower clamp keeping a stub of either side visible.
const SHELL_WIDTH = 296   // inner content area of the locked 320 .j-app shell
const HANDLE_WIDTH = 12

function Demo() {
  const [left, setLeft] = useState(140);
  const maxLeft = SHELL_WIDTH - HANDLE_WIDTH
  const adjust = (delta: number) => setLeft((value) => Math.max(0, Math.min(maxLeft, value + delta)));

  return (
    <div className="j-story-phone j-panel-splitter-story">
      <JimboPanel className="j-panel-splitter-story__shell">
        <div className="j-panel-splitter-story__header">
          <JimboText size="md" tone="white">JAML split view</JimboText>
          <JimboText size="xs" tone="white">Drag the divider — slides all the way</JimboText>
        </div>
        <div className="j-panel-splitter-story__body">
          <section className="j-panel-splitter-story__pane" style={{ flexBasis: left }}>
            {left > 40 && (
              <JimboCodeBlock
                filename="route.jaml"
                language="JAML"
                code={'must:\n  - joker: Blueprint\n  - legendaryJoker: Perkeo\nshould:\n  - voucher: Observatory\n'}
              />
            )}
          </section>
          <JimboPanelSplitter orientation="vertical" onDrag={adjust} onKeyAdjust={adjust} />
          <section className="j-panel-splitter-story__pane j-panel-splitter-story__pane--results">
            {(maxLeft - left) > 40 && (
              <>
                <JimboInfoCard>
                  <JimboInfoCardBody>
                    <JimboInfoCardTitle>Blueprint</JimboInfoCardTitle>
                  </JimboInfoCardBody>
                  <JimboInfoCardAside><JimboBadge tone="green">must</JimboBadge></JimboInfoCardAside>
                </JimboInfoCard>
                <JimboInfoCard>
                  <JimboInfoCardBody>
                    <JimboInfoCardTitle>Perkeo</JimboInfoCardTitle>
                  </JimboInfoCardBody>
                  <JimboInfoCardAside><JimboBadge tone="green">must</JimboBadge></JimboInfoCardAside>
                </JimboInfoCard>
                <JimboInfoCard>
                  <JimboInfoCardBody>
                    <JimboInfoCardTitle>Observatory</JimboInfoCardTitle>
                  </JimboInfoCardBody>
                  <JimboInfoCardAside><JimboBadge tone="blue">should</JimboBadge></JimboInfoCardAside>
                </JimboInfoCard>
              </>
            )}
          </section>
        </div>
      </JimboPanel>
    </div>
  );
}

const meta = {
  title: 'JimboUI / JimboPanelSplitter',
  component: JimboPanelSplitter,
  parameters: {
    jimboHarness: false,
    layout: 'centered',
  },
} satisfies Meta<typeof JimboPanelSplitter>;

export default meta;
type Story = StoryObj<typeof meta>;

export const Vertical: Story = {
  render: () => <Demo />,
};
