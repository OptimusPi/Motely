declare module "@rewaffle/bootsharp-file-system" {
  export function init(mounter: object, debounce?: number): void;

  export function isFileSystemAvailable(): boolean;

  export const hooks: {
    onMountProgress(handler: (progress: number, uri: string) => void): void;
    onSetHandle(handler: (root: string, handle: unknown) => void | Promise<void>): void;
    onGetHandle(handler: (root: string) => unknown | Promise<unknown>): void;
  };
}
