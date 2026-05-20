import type { Meta, StoryObj } from '@storybook/react';
import React, { useMemo, useState } from 'react';
import { JimboPanel } from '../ui/panel';
import { JimboText } from '../ui/jimboText';
import { Jamlyzer } from './Jamlyzer';
import { Motely } from 'motely-wasm';
import type {
  MotelyJamlyzerResult,
  MotelyJamlyzerSeedResult,
} from 'motely-wasm/motely/analysis';
import sampleJaml from './fixtures/jamlyzer-sample.jaml?raw';

const SEEDS = ['ALEEB', 'FROGMANS', 'PILUVYOU'];

type AnalyzeOutcome = {
  result: 'idle' | 'match' | 'nomatch' | 'running' | 'error';
  error: string | null;
  analysis: MotelyJamlyzerResult | null;
};

// Synchronous — bootsharp.boot() ran at preview load (.storybook/preview.tsx).
function runAnalyze(seed: string): AnalyzeOutcome {
  try {
    const validation = Motely.validateJaml(sampleJaml);
    if (validation !== 'valid') {
      throw new Error(String(validation ?? 'Invalid JAML'));
    }
    const data: MotelyJamlyzerResult = Motely.analyzeJamlSeeds(sampleJaml, [seed]);
    if (data.error) {
      throw new Error(data.error);
    }
    const seedResult = data.seeds[0];
    return {
      result: (seedResult?.score ?? 0) >= 1 ? 'match' : 'nomatch',
      error: null,
      analysis: data,
    };
  } catch (e) {
    return {
      result: 'error',
      error: e instanceof Error ? e.message : String(e),
      analysis: null,
    };
  }
}

function JamlyzerDemo() {
  // Prime with the canonical probe seed via lazy state init so the panel is
  // populated without an effect-driven cascade on mount.
  const [outcome, setOutcome] = useState<AnalyzeOutcome>(() => runAnalyze('ALEEB'));
  const { result, error, analysis } = outcome;

  const seeds = useMemo(() => SEEDS, []);

  function analyze(seed: string) {
    setOutcome({ result: 'running', error: null, analysis: null });
    setOutcome(runAnalyze(seed));
  }

  const seedResult: MotelyJamlyzerSeedResult | undefined = analysis?.seeds[0];

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 12, width: 360 }}>
      <JimboPanel>
        <Jamlyzer
          jaml={sampleJaml}
          seeds={seeds}
          initialSeed="ALEEB"
          result={result}
          error={error}
          onTest={analyze}
        />
      </JimboPanel>

      {analysis && (
        <JimboPanel>
          <div style={{ display: 'flex', flexDirection: 'column', gap: 6 }}>
            <JimboText size="sm" tone="gold">Analyzer output</JimboText>
            <JimboText size="xs" tone="grey">
              motely version: {Motely.version()}
            </JimboText>
            <JimboText size="xs" tone="grey">
              seeds searched: {analysis.totalSeedsSearched.toString()} / matching: {analysis.matchingSeeds.toString()}
            </JimboText>
            {seedResult && (
              <>
                <JimboText size="xs" tone="grey">
                  seed: {seedResult.seed} / score: {seedResult.score}
                </JimboText>
                <JimboText size="xs" tone="grey">
                  tallies: [{Array.from(seedResult.tallies).join(', ')}]
                </JimboText>
                <JimboText size="xs" tone="grey">
                  antes analyzed: {seedResult.analysis?.antes.length ?? 0}
                </JimboText>
              </>
            )}
          </div>
        </JimboPanel>
      )}
    </div>
  );
}

const meta = {
  title: 'JAML / Jamlyzer',
  component: Jamlyzer,
} satisfies Meta<typeof Jamlyzer>;

export default meta;
type Story = StoryObj<typeof meta>;

export const Default: Story = {
  render: () => <JamlyzerDemo />,
};
