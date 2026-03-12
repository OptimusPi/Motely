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
var server_exports = {};
__export(server_exports, {
  analyzeSeedServer: () => analyzeSeedServer,
  disposeServerApi: () => disposeServerApi,
  getServerApi: () => getServerApi,
  getServerCapabilities: () => getServerCapabilities,
  startJamlSearchServer: () => startJamlSearchServer
});
module.exports = __toCommonJS(server_exports);
var import_index = require("./index.js");
let apiPromise = null;
let apiOptionsKey = null;
function getOptionsKey(options) {
  return JSON.stringify(options ?? {});
}
function getServerApi(options) {
  const key = getOptionsKey(options);
  if (!apiPromise || apiOptionsKey !== key) {
    apiOptionsKey = key;
    apiPromise = (0, import_index.loadMotely)(options).catch((err) => {
      if (apiOptionsKey === key) {
        apiPromise = null;
        apiOptionsKey = null;
      }
      throw err;
    });
  }
  return apiPromise;
}
async function disposeServerApi() {
  const current = apiPromise;
  apiPromise = null;
  apiOptionsKey = null;
  if (!current) {
    return;
  }
  const api = await current.catch(() => null);
  api?.dispose();
}
async function analyzeSeedServer(seed, deck, stake, options) {
  const api = await getServerApi(options);
  return api.analyzeSeed(seed, deck, stake);
}
async function startJamlSearchServer(jamlContent, options, loadOptions) {
  const api = await getServerApi(loadOptions);
  return api.startJamlSearch(jamlContent, options);
}
async function getServerCapabilities(options) {
  const api = await getServerApi(options);
  return api.getCapabilities();
}
// Annotate the CommonJS export names for ESM import in node:
0 && (module.exports = {
  analyzeSeedServer,
  disposeServerApi,
  getServerApi,
  getServerCapabilities,
  startJamlSearchServer
});
