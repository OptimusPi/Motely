/**
 * Fallback when npm `motely-wasm` has no `dist/` (run `dotnet publish` on Motely.BrowserWasm
 * so `motely-wasm/dist` is populated, then reinstall or use `file:../motely-wasm`).
 */
function stubContext() {
  return {
    beginShopStream(_ante: number) { },
    getNextShopItem(): never {
      throw new Error(
        'Motely WASM not installed: motely-wasm package is missing dist/. Build Motely.BrowserWasm (Bootsharp) and link the motely-wasm folder.'
      )
    },
    dispose() { },
  }
}

export async function boot(): Promise<void> { }

const MotelyBrowserApi = {
  createSingleSearchContext(_seed: string, _deck: string, _stake: string) {
    return stubContext()
  },
  getVersion(): string {
    return 'stub (no WASM dist)'
  },
}

const MotelyWasmBackend = {
  createInstance(): number {
    return 0
  },
  async analyzeSeed(
    _instanceId: number,
    _seed: string,
    _deck: string,
    _stake: string
  ): Promise<string> {
    return JSON.stringify({
      error: 'Motely WASM dist missing — highway uses TS shop stream only.',
    })
  },
}

export const MotelyWasm = {
  MotelyBrowserApi,
  MotelyWasmBackend,
}

export default { boot }
