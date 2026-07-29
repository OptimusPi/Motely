// Custom ESLint rules for jaml-ui design system enforcement.
// Source of truth: CLAUDE.md "Design rules" and AGENTS.md.

const EMOJI_RE =
  /[\u{1F300}-\u{1FAFF}\u{2600}-\u{27BF}\u{1F000}-\u{1F2FF}\u{1F900}-\u{1F9FF}]/u;

// All of jaml-ui is Jimbo. src/ui/ is the primitive layer — it is *made of* raw
// HTML, that is its job. Everywhere else composes Jimbo* primitives and nothing
// else. Banning only <button> while <div> walks free was incoherent: a raw <div>
// is exactly as much un-themed host-rendered HTML as a raw <button>.
// Fragments (<> / <React.Fragment>) are not elements and are fine.
const noRawHtml = {
  meta: {
    type: 'problem',
    docs: { description: 'Only Jimbo* primitives outside src/ui/. No raw HTML.' },
    messages: {
      raw: 'Raw <{{tag}}> is not allowed outside src/ui/. All of jaml-ui is Jimbo — compose a Jimbo* primitive. If the primitive you need does not exist, add it to src/ui/ with a story. See CLAUDE.md "Design rules".',
    },
    schema: [],
  },
  create(context) {
    return {
      JSXOpeningElement(node) {
        const name = node.name;
        // Lowercase JSXIdentifier === an intrinsic HTML element. A capitalized
        // one is a component; a JSXMemberExpression (<Foo.Bar>) is too.
        if (name.type !== 'JSXIdentifier') return;
        if (!/^[a-z]/.test(name.name)) return;
        context.report({ node, messageId: 'raw', data: { tag: name.name } });
      },
    };
  },
};

const noEmojiJsx = {
  meta: {
    type: 'problem',
    docs: { description: 'No emoji in UI. Use react-icons.' },
    messages: {
      emoji: 'Emoji are not allowed in UI text. Use react-icons (react-icons/fi).',
    },
    schema: [],
  },
  create(context) {
    function check(node, value) {
      if (typeof value !== 'string') return;
      if (EMOJI_RE.test(value)) context.report({ node, messageId: 'emoji' });
    }
    return {
      JSXText(node) {
        check(node, node.value);
      },
      Literal(node) {
        // only inside JSX
        const p = node.parent;
        if (!p) return;
        if (
          p.type === 'JSXAttribute' ||
          p.type === 'JSXExpressionContainer' ||
          p.type === 'JSXElement'
        ) {
          check(node, node.value);
        }
      },
      TemplateElement(node) {
        check(node, node.value.cooked);
      },
    };
  },
};

const noUppercaseText = {
  meta: {
    type: 'problem',
    docs: { description: 'No ALL CAPS text in UI.' },
    messages: {
      caps: 'ALL CAPS text is not allowed in UI ("{{snippet}}"). Use normal case.',
    },
    schema: [],
  },
  create(context) {
    // Flag SHOUTING, not acronyms. Single words must be 5+ chars; multi-word
    // ALL CAPS phrases (with whitespace between caps tokens) are always flagged.
    // Allows JAML, SIMD, JSON, HTML, etc.
    const SINGLE = /\b[A-Z]{5,}\b/;
    const MULTI = /\b[A-Z]{2,}\s+[A-Z]{2,}\b/;
    function check(node, value) {
      if (typeof value !== 'string') return;
      const trimmed = value.trim();
      if (!trimmed) return;
      const m = MULTI.exec(trimmed) || SINGLE.exec(trimmed);
      if (m) context.report({ node, messageId: 'caps', data: { snippet: m[0] } });
    }
    return {
      JSXText(node) {
        check(node, node.value);
      },
    };
  },
};

const noBoldStyle = {
  meta: {
    type: 'problem',
    docs: { description: 'No bold font-weight in JSX inline styles.' },
    messages: {
      bold: 'fontWeight bold/700+ is not allowed. Jimbo design uses normal weight.',
    },
    schema: [],
  },
  create(context) {
    function isBoldValue(v) {
      if (v == null) return false;
      if (typeof v === 'string')
        return /^(bold|bolder|[7-9]00)$/i.test(v.trim());
      if (typeof v === 'number') return v >= 700;
      return false;
    }
    return {
      JSXAttribute(node) {
        if (node.name?.name !== 'style') return;
        const expr = node.value?.expression;
        if (!expr || expr.type !== 'ObjectExpression') return;
        for (const prop of expr.properties) {
          if (prop.type !== 'Property') continue;
          const key = prop.key;
          const keyName =
            key.type === 'Identifier'
              ? key.name
              : key.type === 'Literal'
              ? key.value
              : null;
          if (keyName !== 'fontWeight') continue;
          const val = prop.value;
          if (val.type === 'Literal' && isBoldValue(val.value)) {
            context.report({ node: prop, messageId: 'bold' });
          }
        }
      },
    };
  },
};

// `style={{...}}` outside src/ui/ is forbidden. The one legitimate exception
// is computing a CSS custom property from a prop (e.g. style={{ "--j-card-width": `${w}px` }})
// — that's how StandardCard / GameCard / DeckSprite parameterize the sprite
// sheet. Pass-through (`style={style}`, no ObjectExpression) is fine because
// it's the caller's responsibility, not this file's.
const noInlineStyle = {
  meta: {
    type: 'problem',
    docs: { description: 'No inline style={{}} outside src/ui/. Use Jimbo primitives.' },
    messages: {
      inline:
        'Inline style={{}} is not allowed here — compose a Jimbo* primitive or use a .j-* class. The only allowed shape is a single CSS-custom-property assignment (style={{ "--j-foo": ... }}). See CLAUDE.md.',
    },
    schema: [],
  },
  create(context) {
    function isCustomPropertyOnly(obj) {
      if (!obj.properties.length) return false;
      for (const prop of obj.properties) {
        if (prop.type !== 'Property') return false;
        const k = prop.key;
        const name =
          k.type === 'Literal' ? k.value : k.type === 'Identifier' ? k.name : null;
        if (typeof name !== 'string' || !name.startsWith('--')) return false;
      }
      return true;
    }
    return {
      JSXAttribute(node) {
        if (node.name?.name !== 'style') return;
        const expr = node.value?.expression;
        if (!expr || expr.type !== 'ObjectExpression') return;
        if (isCustomPropertyOnly(expr)) return;
        context.report({ node, messageId: 'inline' });
      },
    };
  },
};

// JimboColorOption is a JS color constant for canvas / R3F / SVG — surfaces
// that can't read CSS variables. Using it inside a JSX style={{}} duplicates
// the design tokens and breaks themability. Use the `--j-*` CSS custom
// properties instead.
const noTokenInJsxStyle = {
  meta: {
    type: 'problem',
    docs: { description: 'JimboColorOption belongs in canvas/R3F/SVG, not JSX style.' },
    messages: {
      token:
        'JimboColorOption is for canvas/R3F/SVG only. In JSX, use the matching --j-* CSS custom property.',
    },
    schema: [],
  },
  create(context) {
    let styleDepth = 0;
    return {
      JSXAttribute(node) {
        if (node.name?.name === 'style') styleDepth++;
      },
      'JSXAttribute:exit'(node) {
        if (node.name?.name === 'style') styleDepth--;
      },
      MemberExpression(node) {
        if (styleDepth === 0) return;
        const obj = node.object;
        if (obj.type === 'Identifier' && obj.name === 'JimboColorOption') {
          context.report({ node, messageId: 'token' });
        }
      },
    };
  },
};

// Anonymous helper components inside consumer screens — e.g. `function TallyBar()`
// at the top of JamlIde.tsx — are the seed of design drift. If a piece of UI
// is reusable enough to extract, it belongs in src/ui/ as a Jimbo primitive
// with a story. This rule fires on top-level function/arrow declarations that
// return JSX, in files outside src/ui/.
const noInlineComponent = {
  meta: {
    type: 'problem',
    docs: { description: 'Helper components belong in src/ui/ as Jimbo primitives.' },
    messages: {
      inline:
        'Helper component "{{name}}" should live in src/ui/ as a Jimbo primitive with a story, not inline in a consumer screen.',
    },
    schema: [],
  },
  create(context) {
    function nameLooksLikeComponent(name) {
      return /^[A-Z]/.test(name);
    }
    function bodyReturnsJsx(node) {
      const body = node.body;
      if (!body) return false;
      if (body.type === 'JSXElement' || body.type === 'JSXFragment') return true;
      if (body.type !== 'BlockStatement') return false;
      for (const stmt of body.body) {
        if (stmt.type !== 'ReturnStatement' || !stmt.argument) continue;
        const a = stmt.argument;
        if (a.type === 'JSXElement' || a.type === 'JSXFragment') return true;
        if (
          a.type === 'ConditionalExpression' &&
          (a.consequent.type === 'JSXElement' || a.alternate.type === 'JSXElement')
        )
          return true;
      }
      return false;
    }
    function isExported(node) {
      const p = node.parent;
      return p && (p.type === 'ExportNamedDeclaration' || p.type === 'ExportDefaultDeclaration');
    }
    return {
      FunctionDeclaration(node) {
        if (!node.id || !nameLooksLikeComponent(node.id.name)) return;
        if (isExported(node)) return;
        if (!bodyReturnsJsx(node)) return;
        context.report({ node: node.id, messageId: 'inline', data: { name: node.id.name } });
      },
      VariableDeclaration(node) {
        if (isExported(node)) return;
        for (const decl of node.declarations) {
          if (decl.id.type !== 'Identifier') continue;
          if (!nameLooksLikeComponent(decl.id.name)) continue;
          const init = decl.init;
          if (!init) continue;
          if (init.type !== 'ArrowFunctionExpression' && init.type !== 'FunctionExpression')
            continue;
          if (!bodyReturnsJsx(init)) continue;
          context.report({
            node: decl.id,
            messageId: 'inline',
            data: { name: decl.id.name },
          });
        }
      },
    };
  },
};

// Rule #1: no flex, anywhere in src/. MCP host iframes size flex content
// differently per host, so a flex layout reflows differently depending on where
// the app is embedded. Grid and absolute positioning are deterministic.
// This is the CI mirror of the same rule in .claude/hooks/check-design.mjs.
// Note: gap / justifyContent / alignItems / placeItems are all valid in grid and
// are deliberately NOT flagged.
const noFlex = {
  meta: {
    type: 'problem',
    docs: { description: 'No flex. Use grid or absolute positioning.' },
    messages: {
      flex:
        'flex is not allowed ({{what}}). Rule #1: this UI ships as an MCP app inside host iframes that size flex content differently per host. Use display: grid or absolute positioning — grid + placeItems: "center" to center, gridAutoFlow: "column" for a row. See CLAUDE.md "Design rules".',
    },
    schema: [],
  },
  create(context) {
    const FLEX_PROPS = new Set([
      'flexDirection',
      'flexWrap',
      'flexGrow',
      'flexShrink',
      'flexBasis',
      'flex',
    ]);
    function keyName(k) {
      if (!k) return null;
      return k.type === 'Identifier' ? k.name : k.type === 'Literal' ? k.value : null;
    }
    return {
      JSXAttribute(node) {
        if (node.name?.name !== 'style') return;
        const expr = node.value?.expression;
        if (!expr || expr.type !== 'ObjectExpression') return;
        for (const prop of expr.properties) {
          if (prop.type !== 'Property') continue;
          const name = keyName(prop.key);
          if (typeof name !== 'string') continue;
          if (name === 'display') {
            const v = prop.value;
            if (
              v.type === 'Literal' &&
              typeof v.value === 'string' &&
              /^(inline-)?flex$/.test(v.value.trim())
            ) {
              context.report({
                node: prop,
                messageId: 'flex',
                data: { what: `display: "${v.value}"` },
              });
            }
            continue;
          }
          if (FLEX_PROPS.has(name)) {
            context.report({ node: prop, messageId: 'flex', data: { what: name } });
          }
        }
      },
    };
  },
};

export default {
  rules: {
    'no-flex': noFlex,
    'no-raw-html': noRawHtml,
    'no-emoji-jsx': noEmojiJsx,
    'no-uppercase-text': noUppercaseText,
    'no-bold-style': noBoldStyle,
    'no-inline-style': noInlineStyle,
    'no-token-in-jsx-style': noTokenInJsxStyle,
    'no-inline-component': noInlineComponent,
  },
};
