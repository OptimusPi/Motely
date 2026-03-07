function resolveFrameworkUrl(baseUrl) {
  if (baseUrl) {
    return (baseUrl.endsWith("/") ? baseUrl.slice(0, -1) : baseUrl) || baseUrl;
  }

  return new URL("./_framework/", import.meta.url).href.replace(/\/$/, "");
}

export async function loadMotely(options) {
  const url = resolveFrameworkUrl(options?.baseUrl);
  const dotnetUrl = `${url}/dotnet.js`;

  const { dotnet } = await import(
    /* @vite-ignore */ /* webpackIgnore: true */ dotnetUrl
  );

  const runtime = await dotnet.create();
  const config = runtime.getConfig();
  const allExports = await runtime.getAssemblyExports(config.mainAssemblyName);
  const raw = allExports.Motely.BrowserWasm.MotelyWasmExports;

  runtime.runMain().catch(err => console.error("[motely-wasm-singlethread] runMain failed:", err));

  const [versionJson, capabilitiesJson] = await Promise.all([
    raw.GetVersionAsync(),
    raw.GetCapabilitiesAsync(),
  ]);

  const cachedVersion = JSON.parse(versionJson);
  const cachedCapabilities = JSON.parse(capabilitiesJson);

  return {
    getVersion: () => cachedVersion,
    getCapabilities: () => cachedCapabilities,
    isSimdEnabled: () => cachedCapabilities.simd,
    isThreadingEnabled: () => cachedCapabilities.threads,
    getProcessorCount: () => cachedCapabilities.processorCount,
    async analyzeSeed(seed, deck, stake) {
      const json = await raw.AnalyzeSeedAsync(seed, deck, stake);
      const result = JSON.parse(json);
      if (result.error && !result.seed) {
        throw new Error(result.error);
      }
      return result;
    },
    async validateJaml(jaml) {
      const json = await raw.ValidateJamlAsync(jaml);
      return JSON.parse(json);
    },
    async startJamlSearch(jamlContent, options) {
      const { onProgress, onResult, ...searchParams } = options ?? {};
      const withDefaults = {
        threadCount: cachedCapabilities.processorCount,
        batchCharCount: 4,
        ...searchParams,
      };
      const optionsJson = JSON.stringify(withDefaults);
      const progressCb = onProgress
        ? json => {
            const p = JSON.parse(json);
            onProgress(p.seedsSearched, p.matchingSeeds, p.elapsedMs, p.resultCount);
          }
        : () => {};
      const resultCb = onResult ?? (() => {});
      const resultJson = await raw.StartJamlSearch(jamlContent, optionsJson, progressCb, resultCb);
      const result = JSON.parse(resultJson);
      if (result.error) {
        throw new Error(result.error);
      }
      return result;
    },
    stopSearch: () => {
      raw.StopSearch();
    },
    disposeSearch: () => raw.DisposeSearch(),
  };
}
