export const JAML_LANGUAGE_ID: "jaml";
export const JAML_FILE_EXTENSION: ".jaml";
export const JAML_SCHEMA_ID: "https://www.seedfinder.app/jaml.schema.json";
export const JAML_SCHEMA_PATH: string;
export const JAML_CRITERION_DEFINITION: "JamlClauseDto";
export const JAML_CRITERION_SECTION_KEYS: readonly ["must", "should", "mustNot"];

export interface JamlContract {
  languageId: typeof JAML_LANGUAGE_ID;
  fileExtension: typeof JAML_FILE_EXTENSION;
  schemaId: typeof JAML_SCHEMA_ID;
  schemaPath: typeof JAML_SCHEMA_PATH;
  criterionDefinition: typeof JAML_CRITERION_DEFINITION;
  criterionSectionKeys: typeof JAML_CRITERION_SECTION_KEYS;
}

export const JAML_CONTRACT: Readonly<JamlContract>;

export type JamlDiagnosticSeverity = "error" | "warning" | "information" | "hint";

export interface JamlPosition {
  line: number;
  character: number;
}

export interface JamlRange {
  start: JamlPosition;
  end: JamlPosition;
}

export interface JamlDiagnostic {
  source: "motely" | "jaml-language-core";
  code?: string;
  message: string;
  severity: JamlDiagnosticSeverity;
  range: JamlRange;
  path?: string;
}

export interface MotelyJamlValidationResult {
  valid: boolean;
  message?: string;
  path?: string;
  line: number;
  column: number;
}

export interface MotelyJamlMetaResult {
  antes: ArrayLike<number>;
  itemTypes: string[];
  mustCount: number;
  shouldCount: number;
  mustNotCount: number;
  deck: string;
  stake: string;
}

export interface MotelyJamlValidator {
  validateJamlStructured(jaml: string): MotelyJamlValidationResult;
  getJamlMeta?(jaml: string): MotelyJamlMetaResult;
  getJamlSchema?(): string;
}

export function getJamlSchemaUrl(baseUrl?: string | URL): string;
export function normalizeMotelyValidationResult(result: MotelyJamlValidationResult): JamlDiagnostic[];
export function validateJamlWithMotely(jaml: string, validator: MotelyJamlValidator): JamlDiagnostic[];
export function getJamlMeta(jaml: string, validator: MotelyJamlValidator): MotelyJamlMetaResult | undefined;
export function analyzeJamlText(jaml: string): JamlDiagnostic[];
