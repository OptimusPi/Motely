export let jamlAssetBaseUrl = "";
export let motelyEnums: Record<string, unknown> | null = null;
export let motelyBinPath = "";

export function setJamlAssetBaseUrl(url: string) {
  jamlAssetBaseUrl = url;
}

export function setMotelyBinPath(path: string) {
  motelyBinPath = path;
}

export function setMotelyEnums(motely: Record<string, unknown> | null) {
  motelyEnums = motely;
}

export const setMotelyDisplayEnums = setMotelyEnums;
export const setMotelyDecoderEnums = setMotelyEnums;

export function getJamlAssetBaseUrl() {
  return jamlAssetBaseUrl;
}

export function getMotelyEnums() {
  return motelyEnums;
}
