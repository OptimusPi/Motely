/** Vite alias when `node_modules/motely-wasm/dist` is missing (npm tarball without build output). */
export default {
  async boot(_opts: { root: string | null }): Promise<void> {
    /* no-op — real WASM loads via published motely-wasm with dist + bootsharp */
  },
}
