
const typeKeys = [
  "joker", "souljoker", "voucher",
  "tarot", "tarotcard",
  "planet", "planetcard",
  "spectral", "spectralcard",
  "playingcard", "standardcard",
  "boss", "tag", "smallblindtag", "bigblindtag",
  "erraticrank", "erraticsuit",
  "event", "and", "or"
];

const pluralTypeKeys = [
  "jokers", "souljokers", "vouchers",
  "tarots", "tarotcards",
  "planets", "planetcards",
  "spectrals", "spectralcards",
  "playingcards", "standardcards",
  "bosses", "tags", "smallblindtags", "bigblindtags",
  "erraticranks", "erraticsuits", "events"
];

const inlineArrayProperties = [
  "antes",
  "shopSlots",
  "packSlots",
  "shopslots",
  "packslots",
  "shop_slots",
  "pack_slots",
  "stickers"
];

const validTypesForShorthand = [
  "joker", "souljoker", "tarot", "planet", "spectral", "voucher",
  "tag", "blind", "boss", "card", "playingcard", "standardcard", "event",
  "tarotcard", "planetcard", "spectralcard"
];

function getSingularTypeName(pluralKey) {
  const lower = pluralKey.toLowerCase();
  switch (lower) {
    case "jokers": return "joker";
    case "souljokers": return "soulJoker";
    case "vouchers": return "voucher";
    case "tarots":
    case "tarotcards": return "tarot";
    case "planets":
    case "planetcards": return "planet";
    case "spectrals":
    case "spectralcards": return "spectral";
    case "playingcards":
    case "standardcards": return "playingCard";
    case "bosses": return "boss";
    case "tags": return "tag";
    case "smallblindtags": return "smallBlindTag";
    case "bigblindtags": return "bigBlindTag";
    case "events": return "event";
    case "erraticranks": return "erraticRank";
    case "erraticsuits": return "erraticSuit";
    default: return pluralKey.endsWith('s') ? pluralKey.slice(0, -1) : pluralKey;
  }
}

function normalizeTypeName(typeKey) {
  const lower = typeKey.toLowerCase();
  switch (lower) {
    case "joker": return "Joker";
    case "souljoker": return "SoulJoker";
    case "voucher": return "Voucher";
    case "tarot":
    case "tarotcard": return "TarotCard";
    case "planet":
    case "planetcard": return "PlanetCard";
    case "spectral":
    case "spectralcard": return "SpectralCard";
    case "playingcard":
    case "standardcard": return "PlayingCard";
    case "boss": return "Boss";
    case "smallblindtag": return "SmallBlindTag";
    case "bigblindtag": return "BigBlindTag";
    case "event": return "Event";
    case "erraticrank": return "ErraticRank";
    case "erraticsuit": return "ErraticSuit";
    case "and": return "And";
    case "or": return "Or";
    default: return typeKey.charAt(0).toUpperCase() + typeKey.slice(1);
  }
}

/**
 * Pre-processes JAML string to standard YAML by expanding shorthands.
 * Mirrors JamlConfigLoader.PreProcessJaml in C#
 */
export function preProcessJaml(jamlContent) {
  if (!jamlContent) return "";
  
  const lines = jamlContent.split('\n');
  const result = [];

  for (let i = 0; i < lines.length; i++) {
    const line = lines[i];
    const trimmed = line.trimStart();
    let matched = false;

    if (trimmed.startsWith("- ")) {
      // Handle plural arrays
      for (const pluralKey of pluralTypeKeys) {
        const pattern = `- ${pluralKey}:`;
        if (trimmed.toLowerCase().startsWith(pattern.toLowerCase())) {
          const indent = line.substring(0, line.indexOf('-'));
          const singularType = getSingularTypeName(pluralKey);
          const normalizedType = normalizeTypeName(singularType);
          const arrayContent = trimmed.substring(pattern.length).trim();
          
          result.push(`${indent}- type: ${normalizedType}`);
          result.push(`${indent}  values: ${arrayContent}`);
          matched = true;
          break;
        }
      }

      // Handle singular shorthand
      if (!matched) {
        for (const typeKey of typeKeys) {
          const pattern = `- ${typeKey}:`;
          if (trimmed.toLowerCase().startsWith(pattern.toLowerCase())) {
            const indent = line.substring(0, line.indexOf('-'));
            const value = trimmed.substring(pattern.length).trim();
            const normalizedType = normalizeTypeName(typeKey);
            const lowerTypeKey = typeKey.toLowerCase();

            result.push(`${indent}- type: ${normalizedType}`);

            if (lowerTypeKey === "or" || lowerTypeKey === "and") {
              if (value.toLowerCase() === "null") {
                // do nothing
              } else if (!value) {
                result.push(`${indent}  clauses:`);
              } else {
                result.push(`${indent}  value: ${value}`);
              }
            } else {
              result.push(`${indent}  value: ${value}`);
            }
            matched = true;
            break;
          }
        }
      }
    }

    if (!matched) {
      result.push(line);
    }
  }

  return result.join('\n');
}

/**
 * Post-processes standard YAML to idiomatic JAML with shorthands.
 * Mirrors JamlFormatter.PostProcess in C#
 */
export function postProcessJaml(yaml) {
  if (!yaml) return "";

  const lines = yaml.split('\n');
  const result = [];
  let i = 0;

  while (i < lines.length) {
    const line = lines[i];
    const trimmed = line.trimStart();
    const indent = line.length - trimmed.length;
    const indentStr = ' '.repeat(indent);

    // Type-as-key conversion
    if (trimmed.toLowerCase().startsWith("- type:")) {
      const typeMatch = trimmed.match(/^- type:\s*['"]?(\w+)['"]?\s*$/i);
      if (typeMatch && i + 1 < lines.length) {
        const typeName = typeMatch[1].toLowerCase();
        const nextLine = lines[i + 1];
        const nextTrimmed = nextLine.trimStart();
        
        const valueMatch = nextTrimmed.match(/^value:\s*(.+)$/i);
        if (valueMatch && validTypesForShorthand.includes(typeName)) {
          let value = valueMatch[1].trim();
          if ((value.startsWith("'") && value.endsWith("'")) ||
              (value.startsWith('"') && value.endsWith('"'))) {
            value = value.slice(1, -1);
          }
          
          result.push(`${indentStr}- ${typeName}: ${value}`);
          i += 2;
          continue;
        }
      }
    }

    // Inline numeric arrays
    const arrayPropMatch = trimmed.match(/^(\w+):\s*$/);
    if (arrayPropMatch && inlineArrayProperties.some(p => p.toLowerCase() === arrayPropMatch[1].toLowerCase())) {
      const propName = arrayPropMatch[1];
      const values = [];
      let j = i + 1;
      
      while (j < lines.length) {
        const itemLine = lines[j];
        const itemTrimmed = itemLine.trimStart();
        const itemIndent = itemLine.length - itemTrimmed.length;
        
        if (itemIndent > indent && itemTrimmed.startsWith("- ")) {
          const itemValue = itemTrimmed.slice(2).trim();
          // Simple value check
          if (!isNaN(itemValue) || 
              (itemValue.startsWith("'") && itemValue.endsWith("'")) ||
              (itemValue.startsWith('"') && itemValue.endsWith('"')) ||
              (!itemValue.includes(':') && itemValue.length < 30)) {
            
            const cleanValue = itemValue.replace(/['"]/g, '');
            values.push(cleanValue);
            j++;
            continue;
          }
        }
        break;
      }
      
      if (values.length > 0) {
        result.push(`${indentStr}${propName}: [${values.join(',')}]`);
        i = j;
        continue;
      }
    }

    // Compact already-inline arrays
    const inlineArrayMatch = trimmed.match(/^(\w+):\s*\[\s*(.+?)\s*\]\s*$/);
    if (inlineArrayMatch && inlineArrayProperties.some(p => p.toLowerCase() === inlineArrayMatch[1].toLowerCase())) {
      const propName = inlineArrayMatch[1];
      const arrayContent = inlineArrayMatch[2];
      const compactContent = arrayContent.split(',').map(s => s.trim()).join(',');
      result.push(`${indentStr}${propName}: [${compactContent}]`);
      i++;
      continue;
    }

    result.push(line);
    i++;
  }

  return result.join('\n').trimEnd() + '\n';
}
