import type { Meta, StoryObj } from '@storybook/react';
import { useLayoutEffect, useRef, useState } from 'react';
import { JimboPanelSplitter } from './JimboPanelSplitter';
import { JimboPanel } from './panel';
import { JimboText } from './jimboText';

const HANDLE_WIDTH = 12;

function Demo() {
  const bodyRef = useRef<HTMLDivElement>(null);
  const [trackWidth, setTrackWidth] = useState(0);
  const [left, setLeft] = useState(0);

  useLayoutEffect(() => {
    const el = bodyRef.current;
    if (!el) return;
    const sync = () => {
      const w = el.clientWidth;
      setTrackWidth(w);
      setLeft((prev) => (prev === 0 ? Math.floor(w / 2) : Math.min(prev, Math.max(0, w - HANDLE_WIDTH))));
    };
    sync();
    const ro = new ResizeObserver(sync);
    ro.observe(el);
    return () => ro.disconnect();
  }, []);

  const maxLeft = Math.max(0, trackWidth - HANDLE_WIDTH);
  const adjust = (delta: number) => {
    setLeft((value) => Math.max(0, Math.min(maxLeft, value + delta)));
  };

  const showLeft = left > 40;
  const showRight = maxLeft - left > 40;

  return (
    <div style={{ flex: 1, minHeight: 0, display: 'flex', flexDirection: 'column', width: '100%' }}>
    <JimboPanel className="j-panel-splitter-demo">
      <div ref={bodyRef} className="j-panel-splitter-demo__body">
        <section className="j-panel-splitter-demo__pane" style={{ flexBasis: left }}>
          {showLeft ? (
            <JimboText size="sm" tone="grey">Editor</JimboText>
          ) : null}
        </section>
        <JimboPanelSplitter orientation="vertical" onDrag={adjust} onKeyAdjust={adjust} />
        <section className="j-panel-splitter-demo__pane">
          {showRight ? (
            <JimboText size="sm" tone="grey">Results</JimboText>
          ) : null}
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
    jimboHarness: 'fluid',
    layout: 'fullscreen',
  },
} satisfies Meta<typeof JimboPanelSplitter>;

export default meta;
type Story = StoryObj<typeof meta>;

export const Vertical: Story = {
  render: () => <Demo />,
};
