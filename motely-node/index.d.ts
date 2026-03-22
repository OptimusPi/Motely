export type MotelyNodeModule = {
  createMotely: (options?: { jamlSchemaPath?: string }) => unknown;
};

declare const mod: MotelyNodeModule;
export = mod;
