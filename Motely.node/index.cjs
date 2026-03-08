const import_meta_url=require('url').pathToFileURL(__filename).href;
"use strict";
var __defProp = Object.defineProperty;
var __getOwnPropDesc = Object.getOwnPropertyDescriptor;
var __getOwnPropNames = Object.getOwnPropertyNames;
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
var __toCommonJS = (mod) => __copyProps(__defProp({}, "__esModule", { value: true }), mod);
var index_exports = {};
__export(index_exports, {
  loadMotely: () => loadMotely
});
module.exports = __toCommonJS(index_exports);
var import_node_url = require("node:url");
var import_node_path = require("node:path");
var import_node_url2 = require("node:url");
function _dir() {
  return (0, import_node_path.dirname)((0, import_node_url.fileURLToPath)(import_meta_url));
}
function buildApi(raw, cachedCapabilities) {
  return {
    async getCapabilities() {
      return cachedCapabilities;
    },
    getAvailableThreadCount() {
      return cachedCapabilities.availableThreadCount;
    },
    async analyzeSeed(seed, deck, stake) {
      const json = await raw.AnalyzeSeedAsync(seed, deck, stake);
      const result = JSON.parse(json);
      if (result.error && !result.seed)
        throw new Error(result.error);
      return result;
    },
    async validateJaml(jaml) {
      const json = await raw.ValidateJamlAsync(jaml);
      return JSON.parse(json);
    },
    async startJamlSearch(jamlContent, options) {
      const { onProgress, onResult, ...searchParams } = options ?? {};
      const optionsJson = JSON.stringify({
        threadCount: 1,
        batchCharCount: 4,
        ...searchParams
      });
      const results = [];
      const progressCb = onProgress ? (json) => {
        const p = JSON.parse(json);
        onProgress(p.seedsSearched, p.matchingSeeds, p.elapsedMs, p.resultCount);
      } : () => {
      };
      const resultCb = (seed, score) => {
        results.push({ seed, score });
        onResult?.(seed, score);
      };
      const response = JSON.parse(await raw.StartJamlSearch(jamlContent, optionsJson, progressCb, resultCb));
      if (response.error)
        throw new Error(response.error);
      return results;
    },
    dispose() {
      void raw.DisposeSearch();
    }
  };
}
async function loadMotely(options) {
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
  const capabilitiesJson = await raw.GetCapabilitiesAsync();
  const cachedCapabilities = JSON.parse(capabilitiesJson);
  return buildApi(raw, cachedCapabilities);
}
// Annotate the CommonJS export names for ESM import in node:
0 && (module.exports = {
  loadMotely
});
