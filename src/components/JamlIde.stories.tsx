import type { Meta, StoryObj } from '@storybook/react';
import React, { useMemo, useState } from 'react';
import { JamlIde, type JamlIdeSearchResult } from './JamlIde';
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
    return (
      <JamlIde
        style={{ flex: 1, minHeight: 0 }}
        jaml={jaml}
        onChange={setJaml}
        defaultMode="jamlyzer"
      />
    );
  },
};
