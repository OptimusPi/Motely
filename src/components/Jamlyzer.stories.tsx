import type { Meta, StoryObj } from '@storybook/react';
import React, { useState } from 'react';
import { JimboPanel } from '../ui/panel';
import { Jamlyzer } from './Jamlyzer';
import jamlyzerSampleJaml from './fixtures/jamlyzer-sample.jaml?raw';

function Demo() {
  const [result, setResult] = useState<'idle' | 'match' | 'nomatch' | 'running' | 'error'>('idle');

  return (
    <div style={{ width: 300 }}>
      <JimboPanel>
        <Jamlyzer
          jaml={jamlyzerSampleJaml}
          seeds={['ALEEB', 'FROGMANS', 'PILUVYOU']}
          initialSeed="ALEEB"
          result={result}
          onTest={(seed) => setResult(seed === 'ALEEB' ? 'match' : 'nomatch')}
        />
      </JimboPanel>
    </div>
  );
}

const meta = {
  title: 'JAML / Jamlyzer',
  component: Jamlyzer,
  parameters: {
    jimboHarness: false,
    layout: 'centered',
  },
} satisfies Meta<typeof Jamlyzer>;

export default meta;
type Story = StoryObj<typeof meta>;

export const Default: Story = {
  render: () => <Demo />,
};
