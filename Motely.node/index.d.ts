/** Addon exports from C# [JSExport]. All async methods return JSON strings. */
declare const addon: {
  GetVersionAsync(): Promise<string>;
  GetCapabilitiesAsync(): Promise<string>;
  AnalyzeSeedAsync(seed: string, deck: string, stake: string): Promise<string>;
  ValidateJamlAsync(jamlContent: string): Promise<string>;
  StartJamlSearch(jamlContent: string, optionsJson: string): Promise<string>;
  GetSearchStatus(): Promise<string>;
  ProcessBlockAsync(jamlContent: string, blockId: number): Promise<string>;
  StopSearch(): void;
  DisposeSearch(): Promise<void>;
};
export default addon;
