import type { Meta, StoryObj } from '@storybook/react';
import React, { useMemo, useState } from 'react';
import { JamlIde, type JamlIdeSearchResult } from './JamlIde';
import { Motely } from 'motely-wasm';
import { ensureMotelyReady } from '../lib/motely/runtime';
import { useSearch } from '../hooks/useSearch.js';
import { JimboText } from '../ui/jimboText.js';
import sampleJaml from './fixtures/ide-sample.jaml?raw';

const SEARCH_COUNT = 5_000;

const meta: Meta<typeof JamlIde> = {
  title: 'JAML/JamlIde',
  component: JamlIde,
  parameters: {
    layout: 'fullscreen',
    jimboHarness: true,
  },
};

export default meta;
type Story = StoryObj<typeof JamlIde>;

export const Default: Story = {
  render: () => {
    // eslint-disable-next-line react-hooks/rules-of-hooks
    const [jaml, setJaml] = useState(sampleJaml);
    return (
      <JamlIde
        style={{ flex: 1, minHeight: 0 }}
        jaml={jaml}
        onChange={setJaml}
      />
    );
  },
};

function JamlIdeWithSearchDemo() {
  const [jaml, setJaml] = useState(sampleJaml);
  const { results, status, error, tallyLabels, startRandom, cancel } = useSearch();

  const searchResults: JamlIdeSearchResult[] = useMemo(
    () =>
      results.map((r) => ({
        seed: r.seed,
        score: r.score,
        tallyColumns: r.tallyColumns,
        tallyLabels,
      })),
    [results, tallyLabels],
  );

  const running = status === 'running';

  return (
    <JamlIde
      style={{ flex: 1, minHeight: 0 }}
      jaml={jaml}
      onChange={setJaml}
      searchResults={searchResults}
      onSearch={() => {
        if (running) {
          cancel();
        } else {
          void startRandom(jaml, SEARCH_COUNT);
        }
      }}
      isSearching={running}
      showLoadFileButton
      actions={
        error ? (
          <JimboText size="xs" tone="red">{error}</JimboText>
        ) : null
      }
    />
  );
}

export const WithSearch: Story = {
  render: () => <JamlIdeWithSearchDemo />,
};

export const Jamlyzer: Story = {
  render: () => {
    // eslint-disable-next-line react-hooks/rules-of-hooks
    const [jaml, setJaml] = useState(sampleJaml);
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
