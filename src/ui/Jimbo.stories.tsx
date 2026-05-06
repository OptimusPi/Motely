import type { Meta, StoryObj } from '@storybook/react';
import { useState } from 'react';
import { JimboBackground } from './jimboBackground';
import { JimboApp, JimboAppScroll, JimboAppFooter } from './jimboApp';
import { JimboButton } from './panel';
import { JimboPanel } from './panel';
import { JimboInnerPanel } from './panel';
import { JimboModal } from './panel';
import { JimboText } from './jimboText';
import { JimboTabs, JimboVerticalTabs } from './jimboTabs';
import { JimboToggleList } from './JimboToggleList';
import { JimboFlankNav } from './jimboFlankNav';

const Phone = ({ children }: { children: React.ReactNode }) => (
  <div style={{ width: '100vw', height: '100vh', display: 'flex', alignItems: 'center', justifyContent: 'center', position: 'relative' }}>
    <JimboBackground />
    <div style={{ width: 375, height: 667, position: 'relative', zIndex: 1, overflow: 'hidden', flexShrink: 0 }}>
      {children}
    </div>
  </div>
);

const meta = {
  title: 'JimboUI/Components',
  parameters: { layout: 'fullscreen' },
} satisfies Meta;

export default meta;

export const Typography: StoryObj = {
  render: () => (
    <Phone>
      <JimboApp>
        <JimboAppScroll>
          <JimboPanel>
            <JimboText size="display" tone="gold">Display</JimboText>
            <JimboText size="heading" tone="white">Heading</JimboText>
            <JimboText size="xl" tone="white">Extra Large</JimboText>
            <JimboText size="lg" tone="orange">Large</JimboText>
            <JimboText size="md" tone="white">Medium (body)</JimboText>
            <JimboText size="sm" tone="blue">Small</JimboText>
            <JimboText size="xs" tone="red">Extra Small</JimboText>
            <JimboText size="micro" tone="grey">Micro</JimboText>
            <JimboText size="sm" tone="green">Green</JimboText>
            <JimboText size="sm" tone="purple">Purple</JimboText>
          </JimboPanel>
        </JimboAppScroll>
      </JimboApp>
    </Phone>
  ),
};

export const Buttons: StoryObj = {
  render: () => (
    <Phone>
      <JimboApp>
        <JimboAppScroll>
          <JimboPanel>
            <div className="j-flex j-flex-col j-gap-sm">
              <JimboButton tone="orange" size="lg" fullWidth>Large Orange</JimboButton>
              <JimboButton tone="red" size="md" fullWidth>Medium Red</JimboButton>
              <JimboButton tone="blue" size="md" fullWidth>Medium Blue</JimboButton>
              <JimboButton tone="green" size="sm" fullWidth>Small Green</JimboButton>
              <JimboButton tone="grey" size="sm" fullWidth>Small Grey</JimboButton>
              <div className="j-flex j-gap-sm">
                <JimboButton tone="tarot" size="sm" fullWidth>Tarot</JimboButton>
                <JimboButton tone="planet" size="sm" fullWidth>Planet</JimboButton>
                <JimboButton tone="spectral" size="sm" fullWidth>Spectral</JimboButton>
              </div>
              <JimboButton tone="orange" size="xs" fullWidth>XS Button</JimboButton>
              <JimboButton tone="orange" size="md" fullWidth disabled>Disabled</JimboButton>
            </div>
          </JimboPanel>
        </JimboAppScroll>
      </JimboApp>
    </Phone>
  ),
};

export const Panels: StoryObj = {
  render: () => (
    <Phone>
      <JimboApp>
        <JimboAppScroll>
          <JimboPanel>
            <JimboText size="md" tone="white">JimboPanel — outer container</JimboText>
            <JimboInnerPanel>
              <JimboText size="sm" tone="grey">JimboInnerPanel — nested inset</JimboText>
              <JimboText size="xs" tone="grey">Use for detail blocks inside a panel</JimboText>
            </JimboInnerPanel>
            <JimboInnerPanel>
              <JimboText size="sm" tone="gold">Another inner panel</JimboText>
            </JimboInnerPanel>
          </JimboPanel>
        </JimboAppScroll>
      </JimboApp>
    </Phone>
  ),
};

export const PanelWithBack: StoryObj = {
  render: () => {
    // eslint-disable-next-line react-hooks/rules-of-hooks
    const [view, setView] = useState<'home' | 'detail'>('home');
    return (
      <Phone>
        <JimboApp>
          <JimboAppScroll>
            {view === 'home' ? (
              <JimboPanel>
                <JimboText size="lg" tone="white">Home</JimboText>
                <JimboButton tone="orange" size="md" fullWidth onClick={() => setView('detail')}>Open Detail</JimboButton>
              </JimboPanel>
            ) : (
              <JimboPanel onBack={() => setView('home')}>
                <JimboText size="lg" tone="white">Detail View</JimboText>
                <JimboText size="sm" tone="grey">Back button is always at the bottom.</JimboText>
              </JimboPanel>
            )}
          </JimboAppScroll>
        </JimboApp>
      </Phone>
    );
  },
};

export const Modal: StoryObj = {
  render: () => {
    // eslint-disable-next-line react-hooks/rules-of-hooks
    const [open, setOpen] = useState(false);
    return (
      <Phone>
        <JimboApp>
          <JimboAppScroll>
            <JimboPanel>
              <JimboText size="lg" tone="white">Modal Demo</JimboText>
              <JimboText size="sm" tone="grey">Modal always has Back button. Clicking outside does nothing — only Back closes it.</JimboText>
              <JimboButton tone="orange" size="md" fullWidth onClick={() => setOpen(true)}>Open Modal</JimboButton>
            </JimboPanel>
          </JimboAppScroll>
        </JimboApp>
        <JimboModal open={open} onClose={() => setOpen(false)} title="Pick a Joker">
          <JimboText size="sm" tone="grey">Modal content goes here.</JimboText>
          <JimboInnerPanel>
            <JimboText size="sm" tone="gold">Wee Joker</JimboText>
          </JimboInnerPanel>
          <JimboInnerPanel>
            <JimboText size="sm" tone="white">Blueprint</JimboText>
          </JimboInnerPanel>
        </JimboModal>
      </Phone>
    );
  },
};

export const Tabs: StoryObj = {
  render: () => {
    // eslint-disable-next-line react-hooks/rules-of-hooks
    const [tab, setTab] = useState('a');
    return (
      <Phone>
        <JimboApp>
          <JimboAppScroll>
            <JimboPanel>
              <JimboTabs
                tabs={[
                  { id: 'a', label: 'Visual' },
                  { id: 'b', label: 'JAML' },
                  { id: 'c', label: 'Map' },
                  { id: 'd', label: 'Results' },
                  { id: 'e', label: 'Jimbolate' },
                ]}
                activeTab={tab}
                onTabChange={setTab}
              />
              <JimboText size="sm" tone="grey">Active: {tab}</JimboText>
              <JimboText size="xs" tone="grey">Always red. Triangle bounces on active tab. Scrolls horizontally — never wraps.</JimboText>
            </JimboPanel>
          </JimboAppScroll>
        </JimboApp>
      </Phone>
    );
  },
};

export const VerticalTabs: StoryObj = {
  render: () => {
    // eslint-disable-next-line react-hooks/rules-of-hooks
    const [tab, setTab] = useState('a');
    return (
      <Phone>
        <JimboApp>
          <JimboAppScroll>
            <JimboPanel>
              <div className="j-flex j-gap-md">
                <JimboVerticalTabs
                  tabs={[
                    { id: 'a', label: 'One' },
                    { id: 'b', label: 'Two' },
                    { id: 'c', label: 'Three' },
                  ]}
                  activeTab={tab}
                  onTabChange={setTab}
                />
                <JimboInnerPanel>
                  <JimboText size="sm" tone="grey">Active: {tab}</JimboText>
                </JimboInnerPanel>
              </div>
            </JimboPanel>
          </JimboAppScroll>
        </JimboApp>
      </Phone>
    );
  },
};

export const ToggleList: StoryObj = {
  render: () => {
    // eslint-disable-next-line react-hooks/rules-of-hooks
    const [items, setItems] = useState([
      { id: 'wee', label: 'Wee Joker', on: true },
      { id: 'bp', label: 'Blueprint', on: false },
      { id: 'brainstorm', label: 'Brainstorm', on: false },
    ]);
    return (
      <Phone>
        <JimboApp>
          <JimboAppScroll>
            <JimboToggleList
              title="Jokers"
              items={items}
              onToggle={(id) => setItems(prev => prev.map(i => i.id === id ? { ...i, on: !i.on } : i))}
            />
          </JimboAppScroll>
        </JimboApp>
      </Phone>
    );
  },
};

export const FlankNav: StoryObj = {
  render: () => {
    // eslint-disable-next-line react-hooks/rules-of-hooks
    const seeds = ['ABCD1234', 'WEEJOKER', 'PERKEO99', 'BLUEPRINT'];
    // eslint-disable-next-line react-hooks/rules-of-hooks
    const [i, setI] = useState(0);
    return (
      <Phone>
        <JimboApp>
          <JimboAppScroll>
            <JimboPanel>
              <JimboFlankNav
                canPrev={i > 0}
                canNext={i < seeds.length - 1}
                onPrev={() => setI(p => p - 1)}
                onNext={() => setI(p => p + 1)}
              >
                <JimboText size="lg" tone="gold">{seeds[i]}</JimboText>
              </JimboFlankNav>
              <JimboText size="xs" tone="grey" className="j-text-center">{i + 1} / {seeds.length}</JimboText>
            </JimboPanel>
          </JimboAppScroll>
        </JimboApp>
      </Phone>
    );
  },
};

export const AppShell: StoryObj = {
  render: () => (
    <Phone>
      <JimboApp>
        <JimboAppScroll>
          <JimboPanel>
            <JimboText size="lg" tone="gold">Seed Found!</JimboText>
            <JimboText size="sm" tone="white">WEEJOKER</JimboText>
            <JimboInnerPanel>
              <JimboText size="xs" tone="grey">Ante 1: Wee Joker, Blueprint</JimboText>
            </JimboInnerPanel>
          </JimboPanel>
        </JimboAppScroll>
        <JimboAppFooter>
          <JimboButton tone="orange" size="lg" fullWidth>Search</JimboButton>
        </JimboAppFooter>
      </JimboApp>
    </Phone>
  ),
};
