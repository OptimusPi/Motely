import type { IFileMounter } from "motely-wasm/bootsharp/file-system";

/**
 * Storybook does not install the optional `@rewaffle/bootsharp-file-system` package.
 * `motelyBoot` still dynamic-imports it; this alias satisfies the bundler.
 */
export function init(_mounter: IFileMounter): void {}
