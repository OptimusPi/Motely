// Custom ESLint rules for jaml-ui design system enforcement.
// Source of truth: CLAUDE.md "Design rules" and AGENTS.md.

const EMOJI_RE =
  /[\u{1F300}-\u{1FAFF}\u{2600}-\u{27BF}\u{1F000}-\u{1F2FF}\u{1F900}-\u{1F9FF}]/u;

const noRawButton = {
  meta: {
    type: 'problem',
    docs: { description: 'Use JimboButton instead of raw <button>' },
    messages: {
      raw: 'Raw <{{tag}}> is not allowed outside src/ui/. Use a Jimbo* primitive (e.g. JimboButton). See CLAUDE.md design rules.',
    },
    schema: [],
  },
  create(context) {
    const banned = new Set(['button', 'input', 'select', 'textarea']);
    return {
      JSXOpeningElement(node) {
        const name = node.name;
        if (name.type !== 'JSXIdentifier') return;
        if (!banned.has(name.name)) return;
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

export default {
  rules: {
    'no-raw-button': noRawButton,
    'no-emoji-jsx': noEmojiJsx,
    'no-uppercase-text': noUppercaseText,
    'no-bold-style': noBoldStyle,
  },
};
