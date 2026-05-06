import type { Meta, StoryObj } from '@storybook/react';
import React, { useState } from 'react';
import { JamlIde } from './JamlIde';
import { JimboBackground } from '../ui/jimboBackground';

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
  },
  decorators: [
    (Story) => (
      <div style={{ width: '100vw', height: '100vh', display: 'flex', alignItems: 'center', justifyContent: 'center', position: 'relative' }}>
        <JimboBackground />
        <div style={{ width: 375, height: 667, position: 'relative', zIndex: 1, overflow: 'hidden', flexShrink: 0 }}>
          <Story />
        </div>
      </div>
    ),
  ],
};

export default meta;
type Story = StoryObj<typeof JamlIde>;

export const Default: Story = {
  render: () => {
    // eslint-disable-next-line react-hooks/rules-of-hooks
    const [jaml, setJaml] = useState(SAMPLE_JAML);
    return (
      <JamlIde
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
        jaml={jaml}
        onChange={setJaml}
        onSearch={() => setSearching(s => !s)}
        isSearching={searching}
      />
    );
  },
};

export const Jimbolate: Story = {
  render: () => {
    // eslint-disable-next-line react-hooks/rules-of-hooks
    const [jaml, setJaml] = useState(SAMPLE_JAML);
    // eslint-disable-next-line react-hooks/rules-of-hooks
    const [result, setResult] = useState<'idle' | 'match' | 'nomatch'>('idle');
    return (
      <JamlIde
        jaml={jaml}
        onChange={setJaml}
        defaultMode="jimbolate"
        onTestSeed={(seed) => {
          setResult('running' as never);
          setTimeout(() => setResult(seed.startsWith('A') ? 'match' : 'nomatch'), 600);
        }}
        jimbolateResult={result}
      />
    );
  },
};
