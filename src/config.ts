export let jamlAssetBaseUrl = "";
export let motelyEnums: any = null;

export function setJamlAssetBaseUrl(url: string) {
  jamlAssetBaseUrl = url;
}

export function setMotelyDisplayEnums(motely: any) {
  motelyEnums = motely;
}

export function setMotelyDecoderEnums(motely: any) {
  motelyEnums = motely;
}

export function getJamlAssetBaseUrl() {
  return jamlAssetBaseUrl;
}

export function getMotelyEnums() {
  return motelyEnums;
}
