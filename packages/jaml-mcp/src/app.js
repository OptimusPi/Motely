// Builds the "JAML IDE" MCP App — an interactive UI that an MCP Apps host
// (SEP-1865, modelcontextprotocol/ext-apps) renders in a sandboxed iframe.
//
// Shape required by the MCP Apps spec:
//   - a registered ui:// resource whose text is this HTML, served with
//     mimeType "text/html;profile=mcp-app"
//   - the resource declares _meta.ui.csp.resourceDomains so the host builds a
//     CSP that permits loading from the CDN — omit it and the host blocks all
//     external origins, so nothing loads
//   - the open_jaml_ide tool links to the resource via _meta.ui.resourceUri
//     and returns the seed JAML as structuredContent; the host forwards that
//     to the iframe as a ui/notifications/tool-result notification, surfaced
//     by the @modelcontextprotocol/ext-apps App SDK as `ontoolresult`
//
// The HTML is therefore static — one resource for every call. The per-call
// seed arrives through the tool result, not baked into the markup. jaml-ui,
// React, and the App SDK all load from esm.sh; an importmap pins React so
// jaml-ui shares one copy instead of bundling its own.

export const JAML_UI_VERSION = "0.27.0";
export const REACT_VERSION = "19.2.6";
export const EXT_APPS_VERSION = "1.7.1";

// URI tying the open_jaml_ide tool to its UI resource.
export const IDE_RESOURCE_URI = "ui://jaml-ide/mcp-app.html";

// Origins the host's CSP must allow for the app to load: esm.sh serves the JS
// modules, the CSS, the fonts, and the sprite images — all one origin.
export const IDE_CSP_RESOURCE_DOMAINS = ["https://esm.sh"];

// Minimal valid JAML the IDE falls back to when open_jaml_ide gets no `jaml`.
export const STARTER_JAML = `deck: Magic
stake: White
must:
  - joker: Blueprint
    antes: [1]
should:
  - voucher: Telescope
    antes: [1]
    score: 5
`;

// Embed a string inside a <script> body safely: JSON-encode it and neutralize
// any "</script" / "<!--" sequence that would otherwise end the script element.
function embedJson(value) {
  return JSON.stringify(value).replace(/</g, "\\u003c");
}

export function buildIdeApp() {
  const starter = embedJson(STARTER_JAML);
  const esm = "https://esm.sh";
  const ui = `${esm}/jaml-ui@${JAML_UI_VERSION}`;

  return `<!doctype html>
<html lang="en">
<head>
<meta charset="utf-8" />
<meta name="viewport" content="width=device-width, initial-scale=1" />
<title>JAML IDE</title>
<link rel="stylesheet" href="${ui}/fonts.css" />
<link rel="stylesheet" href="${ui}/jimbo.css" />
<style>
  html, body { margin: 0; height: 100%; background: #1b1b22; }
  #root { display: flex; min-height: 100vh; }
  .jaml-app-status {
    margin: 0; padding: 16px; flex: 1;
    color: #e8e8ef; background: #1b1b22;
    font-family: ui-monospace, SFMono-Regular, Menlo, monospace; font-size: 13px;
    white-space: pre-wrap;
  }
</style>
<script type="importmap">
{
  "imports": {
    "react": "${esm}/react@${REACT_VERSION}",
    "react-dom": "${esm}/react-dom@${REACT_VERSION}",
    "react-dom/client": "${esm}/react-dom@${REACT_VERSION}/client",
    "react/jsx-runtime": "${esm}/react@${REACT_VERSION}/jsx-runtime",
    "jaml-ui": "${ui}?external=react,react-dom",
    "@modelcontextprotocol/ext-apps": "${esm}/@modelcontextprotocol/ext-apps@${EXT_APPS_VERSION}"
  }
}
</script>
</head>
<body>
<div id="root"><p class="jaml-app-status">Loading the JAML IDE&hellip;</p></div>
<script type="module">
const STARTER = ${starter};
const root = document.getElementById("root");
try {
  const React = (await import("react")).default;
  const { useState, useEffect, createElement } = React;
  const { createRoot } = await import("react-dom/client");
  const { JamlIde, setJamlAssetBaseUrl } = await import("jaml-ui");
  const { App } = await import("@modelcontextprotocol/ext-apps");
  setJamlAssetBaseUrl("${ui}/assets/");

  // The seed (from the host's tool-result notification) can arrive before the
  // editor has mounted and subscribed; hold it until the editor is ready.
  let applySeed = null;
  let pendingSeed = null;

  function Ide() {
    const [jaml, setJaml] = useState(STARTER);
    useEffect(() => {
      applySeed = setJaml;
      if (pendingSeed != null) { setJaml(pendingSeed); pendingSeed = null; }
      return () => { applySeed = null; };
    }, []);
    return createElement(JamlIde, {
      jaml,
      onChange: setJaml,
      title: "JAML IDE",
      subtitle: "jaml-ui ${JAML_UI_VERSION}",
      style: { flex: 1, minHeight: 0 },
    });
  }
  createRoot(root).render(createElement(Ide));

  const app = new App({ name: "jaml-ide-app", version: "0.1.0" }, {});
  app.ontoolresult = (result) => {
    const seeded = result && result.structuredContent && result.structuredContent.jaml;
    if (typeof seeded !== "string") return;
    if (applySeed) applySeed(seeded);
    else pendingSeed = seeded;
  };
  await app.connect();
} catch (err) {
  root.innerHTML =
    '<p class="jaml-app-status">Could not load the JAML IDE.\\n\\n' +
    String((err && err.stack) || err).replace(/[<>&]/g, "") + '</p>';
}
</script>
</body>
</html>
`;
}
