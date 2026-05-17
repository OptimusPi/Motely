import type { Meta, StoryObj } from '@storybook/react';
import React, { useState } from 'react';
import { JamlIde } from './JamlIde';
import bootsharp, { Motely } from 'motely-wasm';

async function ensureMotelyReady(): Promise<void> {
  if (bootsharp.getStatus() === bootsharp.BootStatus.Standby) {
    await bootsharp.boot('/motely-wasm/bin');
  }
}

const SAMPLE_JAML = `must:
  - joker: Wee Joker
  - uncommonJoker: Any
    antes: [1]
should:
  - rareJoker: Any
    score: 3
`;

const meta: Meta<typeof JamlIde> = {
  title: 'JAML/JamlIde',
  component: JamlIde,
  parameters: {
    layout: 'fullscreen',
    jimboHarness: 'fluid',
  },
};

export default meta;
type Story = StoryObj<typeof JamlIde>;

export const Default: Story = {
  render: () => {
    // eslint-disable-next-line react-hooks/rules-of-hooks
    const [jaml, setJaml] = useState(SAMPLE_JAML);
    return (
      <JamlIde
        style={{ flex: 1, minHeight: 0 }}
        jaml={jaml}
        onChange={setJaml}
        title="JAML IDE"
        subtitle="Jimbo's Ante Markup Language"
      />
    );
  },
};

export const WithSearch: Story = {
  render: () => {
    // eslint-disable-next-line react-hooks/rules-of-hooks
    const [jaml, setJaml] = useState(SAMPLE_JAML);
    // eslint-disable-next-line react-hooks/rules-of-hooks
    const [searching, setSearching] = useState(false);
    return (
      <JamlIde
        style={{ flex: 1, minHeight: 0 }}
        jaml={jaml}
        onChange={setJaml}
        onSearch={() => setSearching(s => !s)}
        isSearching={searching}
        showLoadFileButton
      />
    );
  },
};

export const Jamlyzer: Story = {
  render: () => {
    // eslint-disable-next-line react-hooks/rules-of-hooks
    const [jaml, setJaml] = useState(SAMPLE_JAML);
    // eslint-disable-next-line react-hooks/rules-of-hooks
    const [result, setResult] = useState<'idle' | 'match' | 'nomatch' | 'running' | 'error'>('idle');
    // eslint-disable-next-line react-hooks/rules-of-hooks
    const [jamlyzerError, setJamlyzerError] = useState<string | null>(null);

    return (
      <JamlIde
        style={{ flex: 1, minHeight: 0 }}
        jaml={jaml}
        onChange={setJaml}
        defaultMode="jamlyzer"
        onTestSeed={(seed) => {
          setResult('running');
          setJamlyzerError(null);
          void (async () => {
            try {
              await ensureMotelyReady();
              const validation = Motely.validateJaml(jaml);
              if (validation !== 'valid') {
                throw new Error(String(validation ?? 'Invalid JAML'));
              }
              const data = Motely.analyzeJamlSeeds(jaml, [seed]);
              if (data.error) {
                throw new Error(data.error);
              }
              const sr = data.seeds[0];
              if (!sr) {
                setResult('nomatch');
                return;
              }
              setResult((sr.score ?? 0) >= 1 ? 'match' : 'nomatch');
            } catch (e) {
              setJamlyzerError(e instanceof Error ? e.message : String(e));
              setResult('error');
            }
          })();
        }}
        jamlyzerResult={result}
        jamlyzerError={jamlyzerError}
      />
    );
  },
};
