import type { Meta, StoryObj } from '@storybook/react';
import { useState } from 'react';
import { JimboAppScroll, JimboAppFooter } from './jimboApp';
import { JimboButton, JimboPanel, JimboInnerPanel, JimboModal } from './panel';
import { JimboText } from './jimboText';
import { JimboTabs, JimboVerticalTabs } from './jimboTabs';
import { JimboToggleList } from './JimboToggleList';
import { JimboFlankNav } from './jimboFlankNav';
import { JimboBadge } from './JimboBadge';
import { JimboInfoCard, JimboInfoCardBody, JimboInfoCardTitle, JimboInfoCardSub, JimboInfoCardAside } from './jimboInfoCard';
import { JimboStatGrid } from './jimboStatGrid';
import { JimboSectionHeader } from './jimboSectionHeader';
import { JimboInset } from './jimboInset';
import { JimboWordmark } from './jimboWordmark';
import { JimboCopyRow } from './jimboCopyRow';
import { JimboSelect } from './JimboSelect';
import { JimboIconButton } from './JimboIconButton';

const meta = {
  title: 'JimboUI/Components',
  parameters: { layout: 'centered' },
} satisfies Meta;

export default meta;

export const Typography: StoryObj = {
  render: () => (
    <JimboAppScroll>
      <JimboPanel>
        <JimboText size="display" tone="gold">Display 26px</JimboText>
        <JimboText size="heading" tone="white">Heading 14px</JimboText>
        <JimboText size="xl" tone="white">Extra Large 24px</JimboText>
        <JimboText size="lg" tone="orange">Large 18px</JimboText>
        <JimboText size="md" tone="white">Medium 14px</JimboText>
        <JimboText size="sm" tone="blue">Small 12px</JimboText>
        <JimboText size="xs" tone="red">Extra Small 10px</JimboText>
        <JimboText size="micro" tone="grey">Micro 8px</JimboText>
        <JimboText size="sm" tone="green">Green</JimboText>
        <JimboText size="sm" tone="purple">Purple</JimboText>
        <JimboText size="sm" tone="gold">Gold</JimboText>
      </JimboPanel>
    </JimboAppScroll>
  ),
};

export const Buttons: StoryObj = {
  render: () => (
    <JimboAppScroll>
      <JimboPanel>
        <div className="j-flex j-flex-col j-gap-sm">
          <JimboButton tone="orange" size="lg" fullWidth>Large Orange (Back)</JimboButton>
          <JimboButton tone="red" size="md" fullWidth>Medium Red (Primary)</JimboButton>
          <JimboButton tone="blue" size="md" fullWidth>Medium Blue (Secondary)</JimboButton>
          <JimboButton tone="green" size="sm" fullWidth>Small Green</JimboButton>
          <JimboButton tone="grey" size="sm" fullWidth>Small Grey</JimboButton>
          <div className="j-flex j-gap-sm">
            <JimboButton tone="tarot" size="sm" fullWidth>Tarot</JimboButton>
            <JimboButton tone="planet" size="sm" fullWidth>Planet</JimboButton>
            <JimboButton tone="spectral" size="sm" fullWidth>Spectral</JimboButton>
          </div>
          <JimboButton tone="orange" size="xs" fullWidth>XS Button</JimboButton>
          <JimboButton tone="red" size="md" fullWidth disabled>Disabled</JimboButton>
        </div>
      </JimboPanel>
    </JimboAppScroll>
  ),
};

export const Badges: StoryObj = {
  render: () => (
    <JimboAppScroll>
      <JimboPanel>
        <JimboText size="sm" tone="grey">Flat — no press, no shadow.</JimboText>
        <div className="j-flex j-gap-sm" style={{ flexWrap: 'wrap' }}>
          <JimboBadge tone="red">Red</JimboBadge>
          <JimboBadge tone="blue">Blue</JimboBadge>
          <JimboBadge tone="green">Green</JimboBadge>
          <JimboBadge tone="orange">Orange</JimboBadge>
          <JimboBadge tone="purple">Purple</JimboBadge>
          <JimboBadge tone="dark">Dark</JimboBadge>
          <JimboBadge tone="grey">Grey</JimboBadge>
        </div>
        <div className="j-flex j-gap-sm" style={{ flexWrap: 'wrap' }}>
          <JimboBadge size="md" tone="blue">Must</JimboBadge>
          <JimboBadge size="md" tone="red">Should</JimboBadge>
          <JimboBadge size="md" tone="green">Match</JimboBadge>
        </div>
      </JimboPanel>
    </JimboAppScroll>
  ),
};

export const Panels: StoryObj = {
  render: () => (
    <JimboAppScroll>
      <JimboPanel>
        <JimboText size="md" tone="white">JimboPanel — outer surface</JimboText>
        <JimboInnerPanel>
          <JimboText size="sm" tone="grey">JimboInnerPanel — recessed</JimboText>
          <JimboText size="xs" tone="grey">Dark blue border, darkest background</JimboText>
        </JimboInnerPanel>
        <JimboInnerPanel>
          <JimboText size="sm" tone="gold">Another inner panel</JimboText>
        </JimboInnerPanel>
      </JimboPanel>
    </JimboAppScroll>
  ),
};

export const PanelWithBack: StoryObj = {
  render: () => {
    // eslint-disable-next-line react-hooks/rules-of-hooks
    const [view, setView] = useState<'home' | 'detail'>('home');
    return (
      <JimboAppScroll>
        {view === 'home' ? (
          <JimboPanel>
            <JimboText size="lg" tone="white">Home</JimboText>
            <JimboText size="xs" tone="grey">Back button appears at bottom when onBack is set.</JimboText>
            <JimboButton tone="red" size="md" fullWidth onClick={() => setView('detail')}>Open Detail</JimboButton>
          </JimboPanel>
        ) : (
          <JimboPanel onBack={() => setView('home')}>
            <JimboText size="lg" tone="white">Detail View</JimboText>
            <JimboText size="sm" tone="grey">Back is always orange, always at bottom.</JimboText>
          </JimboPanel>
        )}
      </JimboAppScroll>
    );
  },
};

export const Modal: StoryObj = {
  render: () => {
    // eslint-disable-next-line react-hooks/rules-of-hooks
    const [open, setOpen] = useState(false);
    return (
      <>
        <JimboAppScroll>
          <JimboPanel>
            <JimboText size="lg" tone="white">Modal Demo</JimboText>
            <JimboText size="sm" tone="grey">Only Back closes it. Clicking outside does nothing.</JimboText>
            <JimboButton tone="red" size="md" fullWidth onClick={() => setOpen(true)}>Open Modal</JimboButton>
          </JimboPanel>
        </JimboAppScroll>
        <JimboModal open={open} onClose={() => setOpen(false)} title="Pick a Joker">
          <JimboText size="sm" tone="grey">Modal content lives here.</JimboText>
          <JimboInnerPanel>
            <JimboText size="sm" tone="gold">Wee Joker</JimboText>
          </JimboInnerPanel>
          <JimboInnerPanel>
            <JimboText size="sm" tone="white">Blueprint</JimboText>
          </JimboInnerPanel>
        </JimboModal>
      </>
    );
  },
};

export const Tabs: StoryObj = {
  render: () => {
    // eslint-disable-next-line react-hooks/rules-of-hooks
    const [tab, setTab] = useState('a');
    return (
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
          <JimboText size="xs" tone="grey">All red. Triangle bounces on active. Scrolls horizontally.</JimboText>
        </JimboPanel>
      </JimboAppScroll>
    );
  },
};

export const VerticalTabs: StoryObj = {
  render: () => {
    // eslint-disable-next-line react-hooks/rules-of-hooks
    const [tab, setTab] = useState('a');
    return (
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
      { id: 'perkeo', label: 'Perkeo', on: true },
    ]);
    return (
      <JimboAppScroll>
        <JimboToggleList
          title="Jokers"
          items={items}
          onToggle={(id) => setItems(prev => prev.map(i => i.id === id ? { ...i, on: !i.on } : i))}
        />
      </JimboAppScroll>
    );
  },
};

export const FlankNav: StoryObj = {
  render: () => {
    const seeds = ['ABCD1234', 'WEEJOKER', 'PERKEO99', 'BLUEPRINT'];
    // eslint-disable-next-line react-hooks/rules-of-hooks
    const [i, setI] = useState(0);
    return (
      <JimboAppScroll>
        <JimboPanel>
          <JimboFlankNav
            canPrev={i > 0}
            canNext={i < seeds.length - 1}
            onPrev={() => setI(p => p - 1)}
            onNext={() => setI(p => p + 1)}
          >
            <JimboText size="display" tone="gold">{seeds[i]}</JimboText>
          </JimboFlankNav>
          <JimboText size="xs" tone="grey" className="j-text-center">{i + 1} / {seeds.length}</JimboText>
        </JimboPanel>
      </JimboAppScroll>
    );
  },
};

export const InfoCards: StoryObj = {
  render: () => (
    <JimboAppScroll>
      <JimboPanel>
        <JimboSectionHeader label="Must" tone="blue" />
        <JimboInfoCard tone="blue">
          <JimboInfoCardBody>
            <JimboInfoCardTitle><JimboText size="sm" tone="white">Wee Joker</JimboText></JimboInfoCardTitle>
            <JimboInfoCardSub><JimboText size="xs" tone="grey">Ante 1</JimboText></JimboInfoCardSub>
          </JimboInfoCardBody>
          <JimboInfoCardAside><JimboBadge tone="blue">✓</JimboBadge></JimboInfoCardAside>
        </JimboInfoCard>
        <JimboSectionHeader label="Should" tone="red" />
        <JimboInfoCard tone="red">
          <JimboInfoCardBody>
            <JimboInfoCardTitle><JimboText size="sm" tone="white">Blueprint</JimboText></JimboInfoCardTitle>
            <JimboInfoCardSub><JimboText size="xs" tone="grey">Ante 2–3</JimboText></JimboInfoCardSub>
          </JimboInfoCardBody>
          <JimboInfoCardAside><JimboBadge tone="red">x2</JimboBadge></JimboInfoCardAside>
        </JimboInfoCard>
        <JimboInfoCard>
          <JimboInfoCardBody>
            <JimboInfoCardTitle><JimboText size="sm" tone="white">Default Card</JimboText></JimboInfoCardTitle>
            <JimboInfoCardSub><JimboText size="xs" tone="grey">No tone</JimboText></JimboInfoCardSub>
          </JimboInfoCardBody>
        </JimboInfoCard>
      </JimboPanel>
    </JimboAppScroll>
  ),
};

export const StatGridAndInset: StoryObj = {
  render: () => (
    <JimboAppScroll>
      <JimboPanel>
        <JimboWordmark title="WEEJOKER" subtitle="Erratic Deck · Gold Stake" />
        <JimboStatGrid items={[
          { value: '847', label: 'Seeds' },
          { value: '0.8%', label: 'Match Rate' },
          { value: '1.2s', label: 'Avg Time' },
        ]} />
        <JimboInset>
          <JimboText size="xs" tone="grey">Searching ante 1–4...</JimboText>
          <JimboText size="xs" tone="green">Found: ABCD1234</JimboText>
          <JimboText size="xs" tone="green">Found: WEEJOKER</JimboText>
        </JimboInset>
      </JimboPanel>
    </JimboAppScroll>
  ),
};

export const CopyRowAndSelect: StoryObj = {
  render: () => {
    // eslint-disable-next-line react-hooks/rules-of-hooks
    const [deck, setDeck] = useState('Erratic');
    return (
      <JimboAppScroll>
        <JimboPanel>
          <JimboCopyRow label="Seed" value="WEEJOKER" />
          <JimboCopyRow value="ABCD1234" />
          <JimboSelect
            value={deck}
            onChange={setDeck}
            options={['Red', 'Blue', 'Erratic', 'Magic', 'Ghost', 'Abandoned']}
          />
        </JimboPanel>
      </JimboAppScroll>
    );
  },
};

export const IconButtons: StoryObj = {
  render: () => (
    <JimboAppScroll>
      <JimboPanel>
        <JimboText size="sm" tone="grey">Square, subtle, for toolbars.</JimboText>
        <div className="j-flex j-gap-sm">
          <JimboIconButton title="Search">🔍</JimboIconButton>
          <JimboIconButton title="Settings">⚙️</JimboIconButton>
          <JimboIconButton title="Copy">📋</JimboIconButton>
          <JimboIconButton title="Close">✕</JimboIconButton>
          <JimboIconButton title="Disabled" disabled>✕</JimboIconButton>
        </div>
        <div className="j-flex j-gap-sm">
          <JimboIconButton size="sm" title="Small">🔍</JimboIconButton>
          <JimboIconButton size="sm" title="Small">⚙️</JimboIconButton>
        </div>
      </JimboPanel>
    </JimboAppScroll>
  ),
};

export const AppShell: StoryObj = {
  render: () => (
    <>
      <JimboAppScroll>
        <JimboPanel>
          <JimboWordmark title="WEEJOKER" subtitle="Red Deck · White Stake" />
          <JimboStatGrid items={[
            { value: '4', label: 'Antes' },
            { value: '12', label: 'Jokers' },
            { value: '3', label: 'Specs' },
          ]} />
          <JimboInnerPanel>
            <JimboText size="xs" tone="grey">Ante 1: Wee Joker, Blueprint</JimboText>
            <JimboText size="xs" tone="grey">Ante 2: Brainstorm</JimboText>
          </JimboInnerPanel>
        </JimboPanel>
      </JimboAppScroll>
      <JimboAppFooter>
        <JimboButton tone="red" size="lg" fullWidth>Search</JimboButton>
      </JimboAppFooter>
    </>
  ),
};
