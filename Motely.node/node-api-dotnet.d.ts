/** Minimal types for optional addon path. See https://microsoft.github.io/node-api-dotnet/features/type-definitions.html */
declare module 'node-api-dotnet' {
  export function require(assemblyPath: string): unknown;
}
