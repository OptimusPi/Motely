/**
 * JAML editor binding — CodeMirror 6 driven by the engine's own language service.
 *
 * `MotelyLsp` is `JamlLanguageService` compiled to wasm: the exact brain `Motely.Lsp` hosts over
 * stdio for the VS Code client. Diagnostics, completion and hover therefore come from the loader
 * and the discriminator registry themselves. There is no schema mirror to regenerate here, and
 * nothing in this file to keep in sync with the engine — that is the whole point of it existing.
 *
 * `JamlSpan` is zero-based on both axes by design (JamlSpan.cs), so the only conversion below is
 * CodeMirror's 1-based line lookup.
 *
 * CodeMirror is *injected*, never imported. Two top-level imports of `@codemirror/view` can
 * resolve to two copies of `@codemirror/state`, and CodeMirror rejects extensions built against
 * a foreign copy ("Unrecognized extension value"). The calling page already pins its CodeMirror;
 * it hands those functions in, so exactly one instance exists.
 */

const SEVERITY_BY_NUMBER = { 1: "error", 2: "warning", 3: "info", 4: "hint" };
const SEVERITY_BY_NAME = {
  error: "error",
  warning: "warning",
  information: "info",
  hint: "hint",
};

// JamlCompletionItem.Kind is a coarse category the shells map onto their own item kinds.
const ITEM_KIND = { key: "property", value: "constant", discriminator: "keyword" };

/** Read a JamlSpan. Bootsharp emits marshalled fields camelCased (JSSerializerGenerator.cs:83). */
function readSpan(span) {
  if (!span || typeof span !== "object") return null;
  const { startLine, startColumn, endLine, endColumn } = span;
  if (![startLine, startColumn, endLine, endColumn].every(Number.isInteger)) return null;
  return { startLine, startColumn, endLine, endColumn };
}

/** JamlSpan.IsEmpty — the value a node carries before it is stamped with a location. */
function isEmptySpan(span) {
  const s = readSpan(span);
  return !s || (s.startLine === 0 && s.startColumn === 0 && s.endLine === 0 && s.endColumn === 0);
}

/** Zero-based line/column -> document offset, clamped so a stale span can never throw. */
function offsetAt(doc, line, column) {
  if (!Number.isInteger(line) || line < 0 || line >= doc.lines) return null;
  const l = doc.line(line + 1);
  return Math.min(l.from + Math.max(0, column), l.to);
}

function spanToRange(doc, span) {
  const s = readSpan(span);
  if (!s) return null;
  const from = offsetAt(doc, s.startLine, s.startColumn);
  const to = offsetAt(doc, s.endLine, s.endColumn);
  if (from === null || to === null) return null;
  return { from, to: Math.max(from, to) };
}

function severityOf(value) {
  if (typeof value === "number") return SEVERITY_BY_NUMBER[value] ?? "error";
  if (typeof value === "string") return SEVERITY_BY_NAME[value.toLowerCase()] ?? "error";
  return "error";
}

/**
 * Engine diagnostics as a CodeMirror lint source.
 *
 * `onStatus(text, kind)` mirrors the headline validity line the pages already show, so the
 * summary and the squiggles can never disagree — both come from this one call.
 */
export function jamlDiagnostics(lsp, onStatus) {
  return (view) => {
    const doc = view.state.doc;
    let raw;
    try {
      raw = lsp.diagnose(doc.toString()) ?? [];
    } catch (err) {
      // A throw here is the engine failing, not the document being invalid — say so plainly
      // rather than painting it as a JAML error on line 1.
      onStatus?.(`language service failed: ${err?.message ?? err}`, "error");
      return [];
    }

    const firstLine = doc.line(1);
    const diagnostics = raw.map((d) => {
      const range = spanToRange(doc, d.span) ?? { from: firstLine.from, to: firstLine.to };
      // A zero-width squiggle paints nothing; widen it by one so the user can see it.
      const to = range.to > range.from ? range.to : Math.min(doc.length, range.from + 1);
      return {
        from: range.from,
        to,
        severity: severityOf(d.severity),
        message: d.message,
        source: d.code,
      };
    });

    const errors = diagnostics.filter((d) => d.severity === "error");
    if (errors.length) onStatus?.(errors.map((d) => `${d.source}: ${d.message}`).join("\n"), "error");
    else onStatus?.("valid ✓", "ok");

    return diagnostics;
  };
}

/** Engine vocabulary as a CodeMirror completion source. */
export function jamlCompletions(lsp) {
  return (context) => {
    const { state, pos } = context;
    const line = state.doc.lineAt(pos);

    let items;
    try {
      items = lsp.complete(state.doc.toString(), line.number - 1, pos - line.from) ?? [];
    } catch {
      return null;
    }
    if (!items.length) return null;

    // The service reports the range to overtype. When it declines to, fall back to the word
    // under the cursor so a typed prefix is replaced rather than duplicated.
    let from = pos;
    const replace = items.find((i) => !isEmptySpan(i.replaceSpan ?? i.ReplaceSpan));
    if (replace) {
      const range = spanToRange(state.doc, replace.replaceSpan ?? replace.ReplaceSpan);
      if (range) from = range.from;
    } else {
      const word = context.matchBefore(/[\w-]*/);
      if (word) from = word.from;
    }

    return {
      from,
      validFor: /^[\w-]*$/,
      options: items.map((i) => ({
        label: i.label ?? i.Label,
        detail: i.detail ?? i.Detail ?? undefined,
        type: ITEM_KIND[(i.kind ?? i.Kind ?? "").toLowerCase()] ?? "text",
      })),
    };
  };
}

/**
 * Engine hover as a CodeMirror tooltip source.
 *
 * The service returns markdown. It is rendered as text, not HTML — a tooltip is not worth a
 * markdown parser, and `textContent` cannot inject anything.
 */
export function jamlHover(lsp) {
  return (view, pos) => {
    const line = view.state.doc.lineAt(pos);
    let info;
    try {
      info = lsp.hover(view.state.doc.toString(), line.number - 1, pos - line.from);
    } catch {
      return null;
    }
    const markdown = info?.markdown ?? info?.Markdown;
    if (!markdown) return null;

    const range = spanToRange(view.state.doc, info.span ?? info.Span) ?? { from: pos, to: pos };
    return {
      pos: range.from,
      end: range.to,
      above: true,
      create() {
        const dom = document.createElement("div");
        dom.className = "cm-jaml-hover";
        dom.textContent = markdown;
        return { dom };
      },
    };
  };
}

/**
 * Every JAML language feature as one array of CodeMirror extensions.
 *
 * Pass the CodeMirror pieces the page already imported. `hoverTooltip` is optional: supply it
 * and hover lights up, omit it and the rest still works.
 */
export function jamlEditorExtensions({
  lsp,
  autocompletion,
  linter,
  lintGutter,
  hoverTooltip,
  onStatus,
  delay = 250,
}) {
  if (!lsp) throw new Error("jamlEditorExtensions: no MotelyLsp — boot() before building the editor");

  const extensions = [
    autocompletion({ override: [jamlCompletions(lsp)], activateOnTyping: true }),
    lintGutter(),
    linter(jamlDiagnostics(lsp, onStatus), { delay }),
  ];
  if (hoverTooltip) extensions.push(hoverTooltip(jamlHover(lsp)));
  return extensions;
}
