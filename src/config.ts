export let jamlAssetBaseUrl = "";
export let motelyEnums: Record<string, unknown> | null = null;

export function setJamlAssetBaseUrl(url: string) {
  jamlAssetBaseUrl = url;
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
