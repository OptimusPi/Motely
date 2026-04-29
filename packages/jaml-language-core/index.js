export const JAML_LANGUAGE_ID = "jaml";
export const JAML_FILE_EXTENSION = ".jaml";
export const JAML_SCHEMA_ID = "https://www.seedfinder.app/jaml.schema.json";
export const JAML_SCHEMA_PATH = "schema/jaml.schema.json";
export const JAML_CRITERION_DEFINITION = "JamlCriterion";
export const JAML_CRITERION_SECTION_KEYS = Object.freeze(["must", "should", "mustNot"]);
export const JAML_CONTRACT = Object.freeze({
  languageId: JAML_LANGUAGE_ID,
  fileExtension: JAML_FILE_EXTENSION,
  schemaId: JAML_SCHEMA_ID,
  schemaPath: JAML_SCHEMA_PATH,
  criterionDefinition: JAML_CRITERION_DEFINITION,
  criterionSectionKeys: JAML_CRITERION_SECTION_KEYS
});

const DEFAULT_RANGE = Object.freeze({
  start: Object.freeze({ line: 0, character: 0 }),
  end: Object.freeze({ line: 0, character: Number.MAX_SAFE_INTEGER })
});

const ANALYSIS_SOURCE = "jaml-language-core";
const ALL_DOCUMENT_RANGE = Object.freeze({
  start: Object.freeze({ line: 0, character: 0 }),
  end: Object.freeze({ line: Number.MAX_SAFE_INTEGER, character: Number.MAX_SAFE_INTEGER })
});

export function getJamlSchemaUrl(baseUrl = import.meta.url) {
  return new URL(JAML_SCHEMA_PATH, baseUrl).toString();
}

export function normalizeMotelyValidationResult(result) {
  if (!result || result.valid) {
    return [];
  }

  return [{
    source: "motely",
    message: result.message || "JAML validation failed.",
    severity: "error",
    range: toRange(result.line, result.column),
    ...(result.path ? { path: result.path } : {})
  }];
}

export function validateJamlWithMotely(jaml, validator) {
  if (!validator || typeof validator.validateJamlStructured !== "function") {
    return [{
      source: "jaml-language-core",
      message: "Motely JAML validator is not available.",
      severity: "error",
      range: DEFAULT_RANGE
    }];
  }

  try {
    return normalizeMotelyValidationResult(validator.validateJamlStructured(jaml));
  } catch (error) {
    return [{
      source: "motely",
      message: error instanceof Error ? error.message : String(error),
      severity: "error",
      range: DEFAULT_RANGE
    }];
  }
}

export function getJamlMeta(jaml, validator) {
  if (!validator || typeof validator.getJamlMeta !== "function") {
    return undefined;
  }
  return validator.getJamlMeta(jaml);
}

export function analyzeJamlText(jaml) {
  const text = typeof jaml === "string" ? jaml : "";
  const diagnostics = [];
  const legendaryAny = /^\s*-\s*legendaryJoker:\s*Any\s*$/mi.test(text)
    || /^\s*legendaryJoker:\s*Any\s*$/mi.test(text);
  const hasAnteZero = hasArrayValue(text, "antes", 0);
  const hasAnteOne = hasArrayValue(text, "antes", 1);
  const boosterPackIndexes = getArrayValues(text, "boosterPacks");
  const hasHieroglyphContext = /\b(hieroglyph|petroglyph)\b/i.test(text);

  if (legendaryAny && hasAnteOne && boosterPackIndexes.includes(0)) {
    diagnostics.push({
      source: ANALYSIS_SOURCE,
      code: "legendary-in-first-buffoon-pack",
      message: "`legendaryJoker: Any` in ante 1 booster pack 0 is valid JAML, but that pack is normally the guaranteed first Buffoon Pack and is expected to return zero legendary results unless you are intentionally testing that invariant.",
      severity: "warning",
      range: findRange(text, "legendaryJoker")
    });
  }

  if (hasAnteOne && boosterPackIndexes.some(index => index > 3)) {
    diagnostics.push({
      source: ANALYSIS_SOURCE,
      code: "wide-ante-one-booster-range",
      message: hasHieroglyphContext
        ? "This wide ante 1 booster pack range appears to rely on advanced ante-rewind or voucher context. Valid JAML; verify this broad search is intentional."
        : "This ante 1 booster pack range includes slots beyond normal ante 1 pack availability. Valid JAML, but it may be broad or advanced unless voucher/ante-rewind context is intentional.",
      severity: "warning",
      range: findRange(text, "boosterPacks")
    });
  }

  if (hasAnteZero) {
    diagnostics.push({
      source: ANALYSIS_SOURCE,
      code: "ante-zero-advanced-state",
      message: "`antes: [0]` is valid advanced Balatro state when ante rewind effects are involved. TIP: Require voucher: Hieroglyph to get here!",
      severity: "information",
      range: findRange(text, "antes")
    });
  }

  return diagnostics;
}

function toRange(line, column) {
  const zeroBasedLine = Number.isFinite(line) && line > 0 ? line - 1 : 0;
  const zeroBasedCharacter = Number.isFinite(column) && column > 0 ? column - 1 : 0;
  return {
    start: { line: zeroBasedLine, character: zeroBasedCharacter },
    end: { line: zeroBasedLine, character: Number.MAX_SAFE_INTEGER }
  };
}

function getArrayValues(text, key) {
  const values = [];
  const pattern = new RegExp(`^\\s*${escapeRegExp(key)}\\s*:\\s*\\[([^\\]]*)\\]`, "gmi");
  for (const match of text.matchAll(pattern)) {
    for (const raw of match[1].split(",")) {
      const value = Number.parseInt(raw.trim(), 10);
      if (Number.isInteger(value)) {
        values.push(value);
      }
    }
  }
  return values;
}

function hasArrayValue(text, key, expected) {
  return getArrayValues(text, key).includes(expected);
}

function findRange(text, token) {
  const index = text.search(new RegExp(escapeRegExp(token), "i"));
  if (index < 0) {
    return ALL_DOCUMENT_RANGE;
  }

  const prefix = text.slice(0, index);
  const line = prefix.split(/\r?\n/).length - 1;
  const lineStart = Math.max(prefix.lastIndexOf("\n"), prefix.lastIndexOf("\r")) + 1;
  return {
    start: { line, character: index - lineStart },
    end: { line, character: Number.MAX_SAFE_INTEGER }
  };
}

function escapeRegExp(value) {
  return value.replace(/[.*+?^${}()|[\]\\]/g, "\\$&");
}
