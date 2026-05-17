export let jamlAssetBaseUrl = "";
export let motelyEnums: any = null;

export function setJamlAssetBaseUrl(url: string) {
  jamlAssetBaseUrl = url;
}

export function setMotelyEnums(motely: any) {
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
