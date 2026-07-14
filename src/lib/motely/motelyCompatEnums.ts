// Mirrors Motely/Enums/MotelyJokers.cs (MotelyJAML repo) so the visual joker
// picker's rarity grouping matches the live engine instead of drifting.
// Only the KEY NAMES matter here (JokerPicker normalizes and matches against
// these), not the numeric values — kept as plain objects, not TS enums, so
// Object.keys() returns exactly these string keys with no reverse-mapping noise.

export const MotelyJokerCommon = {
  Joker: 0, GreedyJoker: 1, LustyJoker: 2, WrathfulJoker: 3, GluttonousJoker: 4,
  JollyJoker: 5, ZanyJoker: 6, MadJoker: 7, CrazyJoker: 8, DrollJoker: 9,
  SlyJoker: 10, WilyJoker: 11, CleverJoker: 12, DeviousJoker: 13, CraftyJoker: 14,
  HalfJoker: 15, CreditCard: 16, Banner: 17, MysticSummit: 18, EightBall: 19,
  Misprint: 20, RaisedFist: 21, ChaostheClown: 22, ScaryFace: 23, AbstractJoker: 24,
  DelayedGratification: 25, GrosMichel: 26, EvenSteven: 27, OddTodd: 28, Scholar: 29,
  BusinessCard: 30, Supernova: 31, RideTheBus: 32, Egg: 33, Runner: 34,
  IceCream: 35, Splash: 36, BlueJoker: 37, FacelessJoker: 38, GreenJoker: 39,
  Superposition: 40, ToDoList: 41, Cavendish: 42, RedCard: 43, SquareJoker: 44,
  RiffRaff: 45, Photograph: 46, ReservedParking: 47, MailInRebate: 48, Hallucination: 49,
  FortuneTeller: 50, Juggler: 51, Drunkard: 52, GoldenJoker: 53, Popcorn: 54,
  WalkieTalkie: 55, SmileyFace: 56, GoldenTicket: 57, Swashbuckler: 58, HangingChad: 59,
  ShootTheMoon: 60,
} as const;

export const MotelyJokerUncommon = {
  JokerStencil: 0, FourFingers: 1, Mime: 2, CeremonialDagger: 3, MarbleJoker: 4,
  LoyaltyCard: 5, Dusk: 6, Fibonacci: 7, SteelJoker: 8, Hack: 9,
  Pareidolia: 10, SpaceJoker: 11, Burglar: 12, Blackboard: 13, SixthSense: 14,
  Constellation: 15, Hiker: 16, CardSharp: 17, Madness: 18, Seance: 19,
  Vampire: 20, Shortcut: 21, Hologram: 22, Cloud9: 23, Rocket: 24,
  MidasMask: 25, Luchador: 26, GiftCard: 27, TurtleBean: 28, Erosion: 29,
  ToTheMoon: 30, StoneJoker: 31, LuckyCat: 32, Bull: 33, DietCola: 34,
  TradingCard: 35, FlashCard: 36, SpareTrousers: 37, Ramen: 38, Seltzer: 39,
  Castle: 40, MrBones: 41, Acrobat: 42, SockAndBuskin: 43, Troubadour: 44,
  Certificate: 45, SmearedJoker: 46, Throwback: 47, RoughGem: 48, Bloodstone: 49,
  Arrowhead: 50, OnyxAgate: 51, GlassJoker: 52, Showman: 53, FlowerPot: 54,
  MerryAndy: 55, OopsAll6s: 56, TheIdol: 57, SeeingDouble: 58, Matador: 59,
  Satellite: 60, Cartomancer: 61, Astronomer: 62, Bootstraps: 63,
} as const;

export const MotelyJokerRare = {
  DNA: 0, Vagabond: 1, Baron: 2, Obelisk: 3, BaseballCard: 4,
  AncientJoker: 5, Campfire: 6, Blueprint: 7, WeeJoker: 8, HitTheRoad: 9,
  TheDuo: 10, TheTrio: 11, TheFamily: 12, TheOrder: 13, TheTribe: 14,
  Stuntman: 15, InvisibleJoker: 16, Brainstorm: 17, DriversLicense: 18, BurntJoker: 19,
} as const;

export const MotelyJokerLegendary = {
  Canio: 0, Triboulet: 1, Yorick: 2, Chicot: 3, Perkeo: 4,
} as const;

export const MotelyJoker = {
  ...MotelyJokerCommon,
  ...MotelyJokerUncommon,
  ...MotelyJokerRare,
  ...MotelyJokerLegendary,
} as const;
