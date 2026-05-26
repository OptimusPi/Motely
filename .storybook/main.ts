import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import type { StorybookConfig } from "@storybook/react-vite";
import type { UserConfig } from "vite";
import { mergeConfig } from "vite";

const storybookDir = dirname(fileURLToPath(import.meta.url));

type RollupExternal = NonNullable<
  NonNullable<NonNullable<UserConfig["build"]>["rollupOptions"]>["external"]
>;

function flattenPlugins(plugins: UserConfig["plugins"]): NonNullable<UserConfig["plugins"]> {
  const out: NonNullable<UserConfig["plugins"]> = [];
  const walk = (p: unknown) => {
    if (p == null) return;
    if (Array.isArray(p)) for (const x of p) walk(x);
    else out.push(p as NonNullable<UserConfig["plugins"]>[number]);
  };
  walk(plugins);
  return out;
}

function stripLibraryDtsPlugins(config: UserConfig): UserConfig {
  const plugins = flattenPlugins(config.plugins).filter((p) => {
    const name =
      p && typeof p === "object" && "name" in p && typeof (p as { name?: unknown }).name === "string"
        ? (p as { name: string }).name
        : "";
    return !name.includes("vite-plugin-dts") && !name.includes("unplugin-dts");
  });

  const build = { ...(config.build ?? {}) };
  delete build.lib;

  const rollupOptions = { ...(build.rollupOptions ?? {}) };
  const prevExternal = rollupOptions.external as RollupExternal | undefined;

  rollupOptions.external = (source: string, importer?: string, isResolved?: boolean): boolean | undefined => {
    if (source === "motely-wasm" || source.startsWith("motely-wasm/")) return false;
    if (prevExternal === undefined) return false;
    if (prevExternal === true) return true;
    if (prevExternal === false) return false;
    if (Array.isArray(prevExternal)) {
      return prevExternal.some((e) =>
        typeof e === "string" ? e === source : e instanceof RegExp ? e.test(source) : false,
      );
    }
    if (typeof prevExternal === "function") {
      return prevExternal(source, importer, isResolved ?? false);
    }
    return false;
  };

  build.rollupOptions = rollupOptions;

  return {
    ...config,
    plugins,
    build,
  };
}

const config: StorybookConfig = {
  stories: ["../src/**/*.stories.@(js|jsx|mjs|ts|tsx)"],
  addons: [
    "@chromatic-com/storybook",
    "@storybook/addon-vitest",
    "@storybook/addon-a11y",
    "@storybook/addon-docs",
    "@storybook/addon-mcp",
  ],
  framework: "@storybook/react-vite",
  staticDirs: [
    "../public",
    { from: "../node_modules/motely-wasm/bin", to: "/motely-wasm/bin" },
  ],
  viteFinal: async (cfg) =>
    mergeConfig(stripLibraryDtsPlugins(cfg), {
      resolve: {
        alias: {
          "@rewaffle/bootsharp-file-system": resolve(storybookDir, "bootsharp-fs-stub.ts"),
        },
      },
      optimizeDeps: {
        include: ["motely-wasm"],
      },
      // Allow Cloudflare-tunnel / arbitrary external hosts to reach the dev
      // server (Vite blocks unknown Host headers by default).
      server: {
        allowedHosts: true,
      },
      build: {
        chunkSizeWarningLimit: 1600,
      },
    }),
};

export default config;
