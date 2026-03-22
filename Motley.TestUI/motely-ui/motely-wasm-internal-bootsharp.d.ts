declare module 'motely-wasm-internal-bootsharp' {
  const bootsharp: { boot: (opts: { root: string | null }) => Promise<void> }
  export default bootsharp
}
