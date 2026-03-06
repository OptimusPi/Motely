"use strict";
var __create = Object.create;
var __defProp = Object.defineProperty;
var __getOwnPropDesc = Object.getOwnPropertyDescriptor;
var __getOwnPropNames = Object.getOwnPropertyNames;
var __getProtoOf = Object.getPrototypeOf;
var __hasOwnProp = Object.prototype.hasOwnProperty;
var __export = (target, all) => {
  for (var name in all)
    __defProp(target, name, { get: all[name], enumerable: true });
};
var __copyProps = (to, from, except, desc) => {
  if (from && typeof from === "object" || typeof from === "function") {
    for (let key of __getOwnPropNames(from))
      if (!__hasOwnProp.call(to, key) && key !== except)
        __defProp(to, key, { get: () => from[key], enumerable: !(desc = __getOwnPropDesc(from, key)) || desc.enumerable });
  }
  return to;
};
var __toESM = (mod, isNodeMode, target) => (target = mod != null ? __create(__getProtoOf(mod)) : {}, __copyProps(
  // If the importer is in node compatibility mode or this is not an ESM
  // file that has been converted to a CommonJS file using a Babel-
  // compatible transform (i.e. "__esModule" has not been set), then set
  // "default" to the CommonJS "module.exports" for node compatibility.
  isNodeMode || !mod || !mod.__esModule ? __defProp(target, "default", { value: mod, enumerable: true }) : target,
  mod
));
var __toCommonJS = (mod) => __copyProps(__defProp({}, "__esModule", { value: true }), mod);
var index_exports = {};
__export(index_exports, {
  loadMotely: () => loadMotely
});
module.exports = __toCommonJS(index_exports);
var import_node_url = require("node:url");
var import_node_path = require("node:path");
var import_node_url2 = require("node:url");
const import_meta = {};
function _dir() {
  if (typeof import_meta !== "undefined" && import_meta.url) {
    return (0, import_node_path.dirname)((0, import_node_url.fileURLToPath)(import_meta.url));
  }
  return typeof __dirname !== "undefined" ? __dirname : ".";
}
function buildApi(raw, cachedCapabilities) {
  return {
    async getCapabilities() {
      return cachedCapabilities;
    },
    async analyzeSeed(seed, deck, stake) {
      const json = await raw.AnalyzeSeedAsync(seed, deck, stake);
      const result = JSON.parse(json);
      if (result.error && !result.seed) throw new Error(result.error);
      return result;
    },
    async validateJaml(jaml) {
      const json = await raw.ValidateJamlAsync(jaml);
      return JSON.parse(json);
    },
    async startJamlSearch(jamlContent, options) {
      const { onProgress, onResult, ...searchParams } = options ?? {};
      const withDefaults = {
        threadCount: 1,
        batchCharCount: 4,
        ...searchParams
      };
      const optionsJson = JSON.stringify(withDefaults);
      const isAddon = typeof raw.GetSearchStatus === "function";
      if (isAddon && (onProgress || onResult)) {
        const startPromise = raw.StartJamlSearch(jamlContent, optionsJson);
        const interval = setInterval(async () => {
          try {
            const statusJson = await raw.GetSearchStatus();
            const status = JSON.parse(statusJson);
            if (status.error) return;
            if (onProgress)
              onProgress(
                status.totalSeedsSearched ?? 0,
                status.matchingSeeds ?? 0,
                status.elapsedMs ?? 0,
                status.results?.length ?? 0
              );
            if (onResult && status.results)
              for (const r of status.results) onResult(r.seed, r.score);
            if (!status.isRunning) clearInterval(interval);
          } catch {
          }
        }, 200);
        try {
          await startPromise;
        } finally {
          clearInterval(interval);
        }
      } else if (isAddon) {
        const resultJson = await raw.StartJamlSearch(jamlContent, optionsJson);
        const result = JSON.parse(resultJson);
        if (result.error) throw new Error(result.error);
      } else {
        globalThis.__motelyOnProgress = onProgress ?? (() => {
        });
        globalThis.__motelyOnResult = onResult ?? (() => {
        });
        try {
          const resultJson = await raw.StartJamlSearch(jamlContent, optionsJson);
          const result = JSON.parse(resultJson);
          if (result.error) throw new Error(result.error);
        } finally {
          globalThis.__motelyOnProgress = () => {
          };
          globalThis.__motelyOnResult = () => {
          };
        }
      }
    },
    dispose: () => {
      void raw.DisposeSearch();
    }
  };
}
async function loadMotely(options) {
  if (options?.addonPath) {
    const dotnet2 = await import("node-api-dotnet");
    const m = dotnet2.require(options.addonPath);
    const raw2 = m.MotelyNodeExports ?? m;
    const [versionJson2, capabilitiesJson2] = await Promise.all([
      raw2.GetVersionAsync(),
      raw2.GetCapabilitiesAsync()
    ]);
    const cachedCapabilities2 = JSON.parse(capabilitiesJson2);
    return buildApi(raw2, cachedCapabilities2);
  }
  globalThis.__motelyOnProgress = () => {
  };
  globalThis.__motelyOnResult = () => {
  };
  const frameworkPath = options?.frameworkPath ?? (0, import_node_path.join)(_dir(), "_framework");
  const dotnetJsPath = (0, import_node_path.join)(frameworkPath, "dotnet.js");
  const dotnetUrl = (0, import_node_url2.pathToFileURL)(dotnetJsPath).href;
  const mod = await import(dotnetUrl);
  const dotnet = mod.dotnet;
  const runtime = await dotnet.withDiagnosticTracing(false).create();
  const config = runtime.getConfig();
  const allExports = await runtime.getAssemblyExports(config.mainAssemblyName);
  const raw = allExports.Motely.BrowserWasm.MotelyWasmExports;
  runtime.runMain?.().catch((err) => console.error("[motely-node] runMain failed:", err));
  const [versionJson, capabilitiesJson] = await Promise.all([
    raw.GetVersionAsync(),
    raw.GetCapabilitiesAsync()
  ]);
  const cachedCapabilities = JSON.parse(capabilitiesJson);
  return buildApi(raw, cachedCapabilities);
}
// Annotate the CommonJS export names for ESM import in node:
0 && (module.exports = {
  loadMotely
});
