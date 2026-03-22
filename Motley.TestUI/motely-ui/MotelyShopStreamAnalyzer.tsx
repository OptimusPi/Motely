import { useState } from 'react'
import type { ShopDeck, ShopStake } from './shopStreamShared'
import { ShopStreamShell } from './ShopStreamShell'
import { useMotelyShopStream } from './useMotelyShopStream'

type Props = Readonly<{ onBack: () => void }>

/** Same UI as TS shop stream, backed by Motely C# WASM <code>analyzeSeed</code> (fixed shopQueue per ante). */
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
        'Raw experiment: same buttons as the TS page, but the list comes from Motely’s analyzeSeed → antes[].shopQueue. That is a finite snapshot from the real game logic in C#, not nextShopItem() in a loop forever.'
      }
      footerNote="Reset re-runs analyzeSeed. +N reveals the next N slots from the current ante’s shopQueue until the snapshot is exhausted."
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
    />
  )
}
