import * as vscode from 'vscode';

// Locally typed — VS Code extensions are CJS, motely-wasm is ESM.
// TypeScript's Node16 resolution refuses `typeof import('motely-wasm')`
// across that boundary, so we spell out the subset we actually use.
interface MotelyModule {
  default: { boot(): Promise<void> };
  MotelyWasm: {
    getVersion(): string;
    validateJaml(jaml: string): string;
    validateJamlStructured(jaml: string): {
      valid: boolean;
      message?: string;
      path?: string;
      line: number;
      column: number;
    };
    getJamlMeta(jaml: string): {
      antes: Int32Array;
      itemTypes: Array<string>;
      mustCount: number;
      shouldCount: number;
      mustNotCount: number;
      deck: string;
      stake: string;
    };
  };
}

let motelyModule: MotelyModule | null = null;
let bootPromise: Promise<void> | null = null;

export async function ensureMotely(): Promise<MotelyModule> {
  if (motelyModule) return motelyModule;
  if (!bootPromise) {
    bootPromise = (async () => {
      const m = await import('motely-wasm') as unknown as MotelyModule;
      await m.default.boot();
      motelyModule = m;
    })();
  }
  await bootPromise;
  return motelyModule!;
}

export function getMotelyStatusBarItem(): vscode.StatusBarItem {
  const item = vscode.window.createStatusBarItem(vscode.StatusBarAlignment.Right, 100);
  item.text = '$(loading~spin) Motely';
  item.tooltip = 'Motely WASM runtime loading...';
  item.show();

  ensureMotely()
    .then((m) => {
      const version = m.MotelyWasm.getVersion();
      item.text = `$(beaker) Motely v${version}`;
      item.tooltip = `Motely WASM v${version} — ready`;
    })
    .catch(() => {
      item.text = '$(error) Motely';
      item.tooltip = 'Motely WASM failed to load';
    });

  return item;
}
