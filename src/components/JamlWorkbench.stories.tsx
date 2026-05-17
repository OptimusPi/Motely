import type { Meta, StoryObj } from '@storybook/react';
import React, { useState } from 'react';
import { JamlCurator } from './JamlCurator';
import { JamlIdeToolbar, type JamlIdeMode } from './JamlIdeToolbar';
import { Jamlyzer } from './Jamlyzer';
import { RunConfigModal } from './RunConfigModal';
import { JimboPanel } from '../ui/panel';
import jamlyzerSampleJaml from './fixtures/jamlyzer-sample.jaml?raw';

function ToolbarDemo() {
  const [mode, setMode] = useState<JamlIdeMode>('code');

  return (
    <div style={{ padding: 10 }}>
      <JimboPanel>
        <JamlIdeToolbar
          mode={mode}
          onModeChange={setMode}
          resultCount={12}
          showResultsTab
          showJamlyzerTab
          onSearch={() => undefined}
          onLoadFile={() => undefined}
        />
      </JimboPanel>
    </div>
  );
}

function JamlyzerDemo() {
  const [result, setResult] = useState<'idle' | 'match' | 'nomatch' | 'running' | 'error'>('idle');

  return (
    <div style={{ padding: 10 }}>
      <JimboPanel>
        <Jamlyzer
          jaml={jamlyzerSampleJaml}
          seeds={['ALEEB', 'FROGMANS', 'PILUVYOU']}
          initialSeed="ALEEB"
          result={result}
          onTest={(seed) => {
            setResult(seed === 'ALEEB' ? 'match' : 'nomatch');
          }}
        />
      </JimboPanel>
    </div>
  );
}

function RunConfigDemo() {
  const [open, setOpen] = useState(true);
  const [deck, setDeck] = useState('Red');
  const [stake, setStake] = useState('White');

  return (
    <>
      {!open ? (
        <div style={{ padding: 10 }}>
          <JimboPanel>
            <button type="button" onClick={() => setOpen(true)} style={{ all: 'unset', cursor: 'pointer' }}>
              Reopen Run Config
            </button>
          </JimboPanel>
        </div>
      ) : null}
      <RunConfigModal
        open={open}
        onClose={() => setOpen(false)}
        deck={deck}
        stake={stake}
        onChange={(nextDeck, nextStake) => {
          setDeck(nextDeck);
          setStake(nextStake);
        }}
      />
    </>
  );
}

const meta = {
  title: 'JAML / Workbench',
  parameters: {
    layout: 'fullscreen',
    jimboHarness: true,
  },
} satisfies Meta;

export default meta;
type Story = StoryObj<typeof meta>;

export const Curator: Story = {
  render: () => <JamlCurator />,
};

export const Toolbar: Story = {
  render: () => <ToolbarDemo />,
};

export const JamlyzerTool: Story = {
  render: () => <JamlyzerDemo />,
};

export const RunConfig: Story = {
  render: () => <RunConfigDemo />,
};
