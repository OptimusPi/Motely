import type { Meta, StoryObj } from '@storybook/react';
import React, { useState } from 'react';
import { JamlIde } from './JamlIde';

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
    layout: 'centered',
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
    const [result, setResult] = useState<'idle' | 'match' | 'nomatch'>('idle');
    return (
      <JamlIde
        style={{ flex: 1, minHeight: 0 }}
        jaml={jaml}
        onChange={setJaml}
        defaultMode="jamlyzer"
        onTestSeed={(seed) => {
          setResult('running' as never);
          setTimeout(() => setResult(seed.startsWith('A') ? 'match' : 'nomatch'), 600);
        }}
        jamlyzerResult={result}
      />
    );
  },
};
