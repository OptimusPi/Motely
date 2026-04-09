import { readFileSync, writeFileSync } from "fs";

// Mirrors FormatUtils.FormatDisplayName — CamelCase → spaced, with special cases.
const SPECIAL = {
  EightBall: "8 Ball", Cloud9: "Cloud 9", OopsAll6s: "Oops! All 6s",
  ToTheMoon: "To the Moon", ToDoList: "To Do List", RiffRaff: "Riff-raff",
  MailInRebate: "Mail In Rebate", SockAndBuskin: "Sock and Buskin",
  DriversLicense: "Driver's License", DirectorsCut: "Director's Cut",
  PlanetX: "Planet X", MrBones: "Mr. Bones", ChaostheClown: "Chaos the Clown",
  ShootTheMoon: "Shoot the Moon", RideTheBus: "Ride the Bus",
  HitTheRoad: "Hit the Road", VerdantLeaf: "Verdant Leaf",
  VioletVessel: "Violet Vessel", CrimsonHeart: "Crimson Heart",
  AmberAcorn: "Amber Acorn", CeruleanBell: "Cerulean Bell",
  TheFool: "The Fool", TheMagician: "The Magician",
  TheHighPriestess: "The High Priestess", TheEmpress: "The Empress",
  TheEmperor: "The Emperor", TheHierophant: "The Hierophant",
  TheLovers: "The Lovers", TheChariot: "The Chariot", TheHermit: "The Hermit",
  TheHangedMan: "The Hanged Man", TheDevil: "The Devil", TheTower: "The Tower",
  TheStar: "The Star", TheMoon: "The Moon", TheSun: "The Sun",
  TheWorld: "The World", TheWheelOfFortune: "The Wheel of Fortune",
  GrosMichel: "Gros Michel", WeeJoker: "Wee Joker",
};

function displayName(enumName) {
  if (SPECIAL[enumName]) return SPECIAL[enumName];
  // CamelCase → space-separated
  return enumName.replace(/([A-Z])/g, (m, c, i) => (i > 0 ? " " : "") + c).trim();
}

function namesFor(arr) {
  const all = new Set(arr);
  for (const v of arr) {
    const d = displayName(v);
    if (d !== v) all.add(d);
  }
  return [...all];
}

const schema = JSON.parse(readFileSync("jaml.schema.json", "utf8"));
const c = schema.definitions.clause.properties;
const col = (p) => p?.enum ?? p?.items?.enum ?? [];

const wildcards    = ["any", "anycommon", "anyuncommon", "anyrare", "anylegendary"];
const jokers       = col(c.joker).filter(v => !wildcards.includes(v));
const vouchers     = col(c.voucher);
const bosses       = col(c.boss);
const tags         = col(c.tag);
const tarots       = col(c.tarotCard);
const spectrals    = col(c.spectralCard);
const planets      = col(c.planetCard);
const decks        = schema.properties.deck.enum;
const stakes       = schema.properties.stake.enum;
const editions     = col(c.edition);
const stickers     = c.stickers.items.enum;
const seals        = col(c.seal);
const enhancements = col(c.enhancement);
const aesthetics   = schema.definitions.JamlAesthetic.enum;

const esc = (arr) =>
  arr.map(v => v.replace(/[.*+?^${}()|[\]\\]/g, "\\$&")).join("|");

const pat = (match, name) => ({ match, name });
const wordPat = (arr, name) => pat(`\\b(${esc(arr)})\\b`, name);

const grammar = {
  scopeName: "source.jaml",
  name: "JAML",
  patterns: [
    "#comments", "#keys", "#booleans", "#numbers", "#strings",
    "#wildcards", "#jokers", "#vouchers", "#bosses", "#tags",
    "#tarots", "#spectrals", "#planets", "#deckStake",
    "#editions", "#modifiers", "#aesthetics",
  ].map(i => ({ include: i })),
  repository: {
    comments:   { patterns: [pat("#.*$", "comment.line.number-sign.jaml")] },
    keys:       { patterns: [{ match: "^(\\s*)([A-Za-z_][A-Za-z0-9_-]*)(\\s*:)", captures: { "2": { name: "entity.name.tag.jaml" }, "3": { name: "punctuation.separator.key-value.jaml" } } }] },
    booleans:   { patterns: [pat("\\b(true|false|null)\\b", "constant.language.boolean.jaml")] },
    numbers:    { patterns: [pat("\\b-?\\d+(?:\\.\\d+)?\\b", "constant.numeric.jaml")] },
    strings:    { patterns: [
      {
        begin: '"',
        end: '"',
        name: "string.quoted.double.jaml",
        patterns: [
          { include: "#wildcards" },
          { include: "#jokers" },
          { include: "#vouchers" },
          { include: "#bosses" },
          { include: "#tags" },
          { include: "#tarots" },
          { include: "#spectrals" },
          { include: "#planets" },
          { include: "#deckStake" },
          { include: "#editions" },
          { include: "#modifiers" },
          { include: "#aesthetics" },
        ],
      },
      {
        begin: "'",
        end: "'",
        name: "string.quoted.single.jaml",
        patterns: [
          { include: "#wildcards" },
          { include: "#jokers" },
          { include: "#vouchers" },
          { include: "#bosses" },
          { include: "#tags" },
          { include: "#tarots" },
          { include: "#spectrals" },
          { include: "#planets" },
          { include: "#deckStake" },
          { include: "#editions" },
          { include: "#modifiers" },
          { include: "#aesthetics" },
        ],
      },
    ] },
    wildcards:  { patterns: [wordPat(wildcards,                  "keyword.other.wildcard.jaml")] },
    jokers:     { patterns: [wordPat(namesFor(jokers),           "entity.name.function.joker.jaml")] },
    vouchers:   { patterns: [wordPat(namesFor(vouchers),         "support.function.voucher.jaml")] },
    bosses:     { patterns: [wordPat(namesFor(bosses),           "entity.name.type.boss.jaml")] },
    tags:       { patterns: [wordPat(namesFor(tags),             "variable.other.tag.jaml")] },
    tarots:     { patterns: [wordPat(namesFor(tarots),           "string.other.tarot.jaml")] },
    spectrals:  { patterns: [wordPat(namesFor(spectrals),        "string.other.spectral.jaml")] },
    planets:    { patterns: [wordPat(namesFor(planets),          "string.other.planet.jaml")] },
    deckStake:  { patterns: [wordPat([...decks, ...stakes],      "support.constant.jaml")] },
    editions:   { patterns: [wordPat(namesFor(editions),         "markup.bold.edition.jaml")] },
    modifiers:  { patterns: [wordPat(namesFor([...stickers, ...seals, ...enhancements]), "markup.italic.modifier.jaml")] },
    aesthetics: { patterns: [wordPat(aesthetics,                 "keyword.control.aesthetic.jaml")] },
  },
};

const out = "tools/jaml-language/vscode-extension/syntaxes/jaml.tmLanguage.json";
writeFileSync(out, JSON.stringify(grammar, null, 2));
console.log("Written:", out);
