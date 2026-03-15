/** Types for .NET WASM boot module (emitted by dotnet publish into _framework/). Copied to _framework/dotnet.d.ts before build. */
export const dotnet: {
  withDiagnosticTracing(enabled: boolean): {
    create(): Promise<{
      getAssemblyExports(assemblyName: string): Promise<unknown>;
      getConfig(): { mainAssemblyName: string };
      runMain(): Promise<void>;
    }>;
  };
  create(): Promise<{
    getAssemblyExports(assemblyName: string): Promise<unknown>;
    getConfig(): { mainAssemblyName: string };
    runMain(): Promise<void>;
  }>;
};
