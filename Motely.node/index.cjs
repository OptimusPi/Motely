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
function buildApi(raw, cachedCapabilities, pollIntervalMs) {
  let disposed = false;
  return {
    async getCapabilities() {
      return cachedCapabilities;
    },
    getAvailableThreadCount() {
      return cachedCapabilities.availableThreadCount;
    },
    async analyzeSeed(seed, deck, stake) {
      const json = await raw.analyzeSeedAsync(seed, deck, stake);
      const result = JSON.parse(json);
      if (result.error && !result.seed)
        throw new Error(result.error);
      return result;
    },
    async validateJaml(jaml) {
      const json = await raw.validateJamlAsync(jaml);
      return JSON.parse(json);
    },
    async startJamlSearch(jamlContent, options) {
      if (disposed) {
        throw new Error("Motely instance has been disposed");
      }
      const { onProgress, onResult, ...searchParams } = options ?? {};
      const optionsJson = JSON.stringify({
        threadCount: Math.max(1, searchParams.threadCount ?? cachedCapabilities.availableThreadCount ?? 1),
        batchCharCount: 4,
        palindrome: searchParams.palindrome ?? !(searchParams.specificSeed || searchParams.randomSeeds),
        ...searchParams
      });
      const results = [];
      const seen = /* @__PURE__ */ new Set();
      const applyStatus = (status) => {
        onProgress?.(status.totalSeedsSearched, status.matchingSeeds, status.elapsedMs, status.resultCount);
        for (const result of status.results ?? []) {
          const key = `${result.seed}:${result.score}`;
          if (seen.has(key))
            continue;
          seen.add(key);
          results.push(result);
          onResult?.(result.seed, result.score);
        }
      };
      const completionPromise = raw.startJamlSearch(jamlContent, optionsJson).then((json) => ({ kind: "done", json })).catch((error) => ({ kind: "error", error }));
      while (true) {
        const next = await Promise.race([
          completionPromise,
          new Promise((resolve) => setTimeout(() => resolve({ kind: "tick" }), pollIntervalMs))
        ]);
        if (next.kind === "error") {
          throw next.error;
        }
        if (next.kind === "done") {
          const status2 = JSON.parse(next.json);
          if (status2.error)
            throw new Error(status2.error);
          applyStatus(status2);
          return results;
        }
        const statusJson = await raw.getSearchStatus();
        const status = JSON.parse(statusJson);
        if (status.error) {
          if (status.error === "No active search")
            continue;
          throw new Error(status.error);
        }
        applyStatus(status);
      }
    },
    dispose() {
      disposed = true;
      try {
        raw.stopSearch();
      } catch {
      }
      void raw.disposeSearch();
    }
  };
}
async function loadMotely(options) {
  const addonModulePath = options?.addonPath ?? (options?.frameworkPath ? (0, import_node_path.join)(options.frameworkPath, "Motely.NodeAddon.mjs") : (0, import_node_path.join)(_dir(), "addon", "Motely.NodeAddon.mjs"));
  const addonUrl = (0, import_node_url2.pathToFileURL)(addonModulePath).href;
  const mod = await import(addonUrl);
  const raw = mod.MotelyNodeExports;
  const capabilitiesJson = await raw.getCapabilitiesAsync();
  const cachedCapabilities = JSON.parse(capabilitiesJson);
  return buildApi(raw, cachedCapabilities, Math.max(25, options?.pollIntervalMs ?? 100));
}
// Annotate the CommonJS export names for ESM import in node:
0 && (module.exports = {
  loadMotely
});
