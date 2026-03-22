import { useState } from 'react'
import type { ShopDeck, ShopStake } from './shopStreamShared'
import { ShopStreamShell } from './ShopStreamShell'
import { useTsShopStream } from './useTsShopStream'

type Props = Readonly<{ onBack: () => void }>

export function ShopStreamAnalyzer({ onBack }: Props) {
  const [seed, setSeed] = useState('BALATRO1')
  const [deck, setDeck] = useState<ShopDeck>('Red')
  const [stake, setStake] = useState<ShopStake>('White')
  const [ante, setAnte] = useState(1)

  const engine = useTsShopStream(seed, deck, stake, ante)

  return (
    <ShopStreamShell
      onBack={onBack}
      title="Shop stream — TypeScript engine"
      subtitle={
        'Under the hood each pseudoseed queue is a chained double (Lua-style) stored in Cache — same kind of cursor Motely keeps per stream. nextShopItem advances those cursors; you can snapshot JSON and restore with importStreamState if you wire it.'
      }
      footerNote="Reset stream rebuilds the TS Game. Changing ante clears the list; each pull advances nextShopItem for the current ante. Same virtualized infinite scroll as the Motely page (+100 near bottom)."
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
      cacheMetaTs={engine.cacheMeta}
      cacheMetaMotely={null}
      resetStream={engine.resetStream}
      pull={engine.pull}
      copyDebug={engine.copyDebug}
      copyDebugLabel={engine.copyDebugLabel}
    />
  )
}
