import { useState } from 'react'
import type { ShopDeck, ShopStake } from './shopStreamShared'
import { ShopStreamShell } from './ShopStreamShell'
import { useMotelyShopStream } from './useMotelyShopStream'

type Props = Readonly<{ onBack: () => void }>

/** Same UI as TS shop stream, backed by Motely C# WASM (<code>SeedRouterTests</code> shop stream, not <code>analyzeSeed</code>). */
export function MotelyShopStreamAnalyzer({ onBack }: Props) {
  const [seed, setSeed] = useState('BALATRO1')
  const [deck, setDeck] = useState<ShopDeck>('Red')
  const [stake, setStake] = useState<ShopStake>('White')
  const [ante, setAnte] = useState(1)

  const engine = useMotelyShopStream(seed, deck, stake, ante)

  return (
    <ShopStreamShell
      onBack={onBack}
      title="Shop stream — Motely (C# / WASM)"
      subtitle={
        'Same controls as the TS page, but each row is one C# call to GetNextShopItem on a live shop stream (BeginShopStream per ante). Matches the SeedRouterTests contract, not a JSON shopQueue snapshot.'
      }
      footerNote="Reset rebuilds the WASM search context. Changing ante restarts the shop stream for that ante. The list is virtualized; infinite scroll loads +100 near the bottom. Large timing runs use silent WASM drains (no React rows), then BeginShopStream resets the stream."
      seed={seed}
      setSeed={setSeed}
      deck={deck}
      setDeck={setDeck}
      stake={stake}
      setStake={setStake}
      ante={ante}
      setAnte={setAnte}
      rows={engine.rows}
      streamError={engine.streamError}
      streamReady={engine.streamReady}
      engineLoading={engine.engineLoading}
      cacheMetaTs={null}
      cacheMetaMotely={engine.cacheMeta}
      resetStream={engine.resetStream}
      pull={engine.pull}
      copyDebug={engine.copyDebug}
      copyDebugLabel={engine.copyDebugLabel}
      motelyBenchTwoK={engine.motelyBenchTwoK}
      motelyBenchSizes={engine.motelyBenchSizes}
      motelyBenchRunning={engine.motelyBenchRunning}
      onMotelyBenchTwoK={engine.runMotelyBenchTwoK}
      onMotelyBenchSizes={engine.runMotelyBenchSizes}
    />
  )
}
