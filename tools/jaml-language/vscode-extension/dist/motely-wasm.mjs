/** Fetches main dotnet module (<code>dotnet.js</code>). */
async function getMain(root) {
    if (root == null)
        return await Promise.resolve().then(function () { return dotnet_g; });
    return await import(/*@vite-ignore*/ /*webpackIgnore:true*/ `${root}/dotnet.js`);
}
/** Fetches dotnet native module (<code>dotnet.native.js</code>). */
async function getNative(root) {
    if (root == null)
        return await Promise.resolve().then(function () { return dotnet_native_g; });
    return await import(/*@vite-ignore*/ /*webpackIgnore:true*/ `${root}/dotnet.native.js`);
}
/** Fetches dotnet runtime module (<code>dotnet.runtime.js</code>). */
async function getRuntime(root) {
    if (root == null)
        return await Promise.resolve().then(function () { return dotnet_runtime_g; });
    return await import(/*@vite-ignore*/ /*webpackIgnore:true*/ `${root}/dotnet.runtime.js`);
}

var generated = {
    wasm: { name: "dotnet.native.wasm", content: undefined },
    assemblies: [
        
    ],
    entryAssemblyName: "Motely.BrowserWasm.dll"
};

/** Resources required to boot .NET runtime. */
const resources = generated;

const lookup = new Uint8Array([0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 62, 0, 62, 0, 63, 52, 53, 54, 55, 56, 57, 58, 59, 60, 61, 0, 0, 0, 0, 0, 0, 0, 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 0, 0, 0, 0, 63, 0, 26, 27, 28, 29, 30, 31, 32, 33, 34, 35, 36, 37, 38, 39, 40, 41, 42, 43, 44, 45, 46, 47, 48, 49, 50, 51]);
function decodeBase64(source) {
    if (typeof window === "object")
        return decodeWithBrowser(source);
    if (typeof process === "object")
        return decodeWithNode(source);
    return decodeNaive(source);
}
function decodeWithBrowser(source) {
    const binaryString = window.atob(source);
    const length = binaryString.length;
    const buffer = new ArrayBuffer(length);
    const uint8Array = new Uint8Array(buffer);
    for (let i = 0; i < length; i++)
        uint8Array[i] = binaryString.charCodeAt(i);
    return buffer;
}
function decodeWithNode(source) {
    const buffer = Buffer.from(source, "base64");
    return buffer.buffer.slice(buffer.byteOffset, buffer.byteOffset + buffer.byteLength);
}
function decodeNaive(source) {
    const srcLen = source.length;
    const padLen = (source[srcLen - 2] === "=" ? 2 : (source[srcLen - 1] === "=" ? 1 : 0));
    const outLen = ((srcLen - padLen) * 3) >> 2;
    const buffer = new Uint8Array(outLen);
    let tmp;
    let byteIndex = 0;
    for (let i = 0, baseLen = srcLen - padLen; i < baseLen; i += 4) {
        tmp = (lookup[source.charCodeAt(i)] << 18)
            | (lookup[source.charCodeAt(i + 1)] << 12)
            | (lookup[source.charCodeAt(i + 2)] << 6)
            | (lookup[source.charCodeAt(i + 3)]);
        buffer[byteIndex++] = (tmp >> 16) & 0xFF;
        buffer[byteIndex++] = (tmp >> 8) & 0xFF;
        buffer[byteIndex++] = tmp & 0xFF;
    }
    if (padLen === 1) {
        tmp = (lookup[source.charCodeAt(srcLen - 4)] << 18)
            | (lookup[source.charCodeAt(srcLen - 3)] << 12)
            | (lookup[source.charCodeAt(srcLen - 2)] << 6);
        buffer[byteIndex++] = (tmp >> 16) & 0xFF;
        buffer[byteIndex++] = (tmp >> 8) & 0xFF;
    }
    else if (padLen === 2) {
        tmp = (lookup[source.charCodeAt(srcLen - 4)] << 18)
            | (lookup[source.charCodeAt(srcLen - 3)] << 12);
        buffer[byteIndex++] = (tmp >> 16) & 0xFF;
    }
    return buffer.buffer;
}

/** Builds .NET runtime configuration.
 *  @param resources Resources required for runtime initialization.
 *  @param root When specified, assumes boot resources are side-loaded from the specified root. */
async function buildConfig(resources, root) {
    const embed = root == null;
    const assets = await Promise.all([
        resolveWasm(),
        resolveModule("dotnet.js", "js-module-dotnet", embed ? getMain : undefined),
        resolveModule("dotnet.native.js", "js-module-native", embed ? getNative : undefined),
        resolveModule("dotnet.runtime.js", "js-module-runtime", embed ? getRuntime : undefined),
        ...resources.assemblies.map(resolveAssembly)
    ]);
    const mt = !embed && (await Promise.resolve().then(function () { return dotnet_g; })).mt;
    if (mt)
        assets.push(await resolveModule("dotnet.native.worker.mjs", "js-module-threads"));
    return { assets, mainAssemblyName: resources.entryAssemblyName };
    async function resolveWasm() {
        return {
            name: resources.wasm.name,
            buffer: await resolveBuffer(resources.wasm),
            behavior: "dotnetwasm"
        };
    }
    async function resolveModule(name, behavior, embed) {
        return {
            name,
            moduleExports: embed ? await embed() : undefined,
            behavior
        };
    }
    async function resolveAssembly(res) {
        return {
            name: res.name,
            buffer: await resolveBuffer(res),
            behavior: "assembly"
        };
    }
    async function resolveBuffer(res) {
        if (typeof res.content === "string")
            return decodeBase64(res.content);
        if (res.content !== undefined)
            return res.content.buffer;
        if (!embed)
            return fetchBuffer(res);
        throw Error(`Failed to resolve '${res.name}' boot resource.`);
    }
    async function fetchBuffer(res) {
        const path = `${root}/${res.name}`;
        if (typeof window === "object")
            return (await fetch(path)).arrayBuffer();
        if (typeof process === "object") {
            const { readFile } = await import('fs/promises');
            const bin = await readFile(path);
            return bin.buffer.slice(bin.byteOffset, bin.byteOffset + bin.byteLength);
        }
        throw Error(`Failed to fetch '${path}' boot resource: unsupported runtime.`);
    }
}

let exports$1;
async function bindExports(runtime, assembly) {
    const asm = await runtime.getAssemblyExports(assembly);
    exports$1 = asm["Bootsharp"]?.["Generated"]["Interop"];
}

/** Allows attaching handlers and broadcasting events. */
class Event {
    handlers = new Map();
    warn;
    lastArgs;
    /** Creates new event instance. */
    constructor(options) {
        this.warn = options?.warn ?? console.warn;
    }
    /** Notifies attached handlers with specified payload.
     *  @param args The payload of the notification. */
    broadcast(...args) {
        this.lastArgs = args;
        for (const handler of this.handlers.values())
            handler(...this.lastArgs);
    }
    /** Attaches specified handler for events emitted by this event instance.
     *  @param handler The handler to attach. */
    subscribe(handler) {
        const id = this.getOrDefineId(handler);
        this.subscribeById(id, handler);
        return id;
    }
    /** Detaches specified handler from events emitted by this event instance.
     *  @param handler The handler to detach. */
    unsubscribe(handler) {
        if (handler == null)
            return;
        const id = this.getOrDefineId(handler);
        this.unsubscribeById(id);
    }
    /** Attaches handler with specified identifier for events emitted by this event instance.
     *  @param id Identifier of the handler.
     *  @param handler The handler to attach. */
    subscribeById(id, handler) {
        if (this.handlers.has(id))
            this.warn(`Failed to subscribe event handler with ID '${id}': handler is already subscribed.`);
        else
            this.handlers.set(id, handler);
    }
    /** Detaches handler with specified identifier from events emitted by this event instance.
     *  @param id Identifier of the handler. */
    unsubscribeById(id) {
        if (this.handlers.has(id))
            this.handlers.delete(id);
        else
            this.warn(`Failed to unsubscribe event handler with ID '${id}': handler is not subscribed.`);
    }
    /** In case event was broadcast at least once, returns last payload; undefined otherwise. */
    get last() {
        return this.lastArgs;
    }
    getOrDefineId(handler) {
        const prop = "bootsharpEventHandlerId";
        if (handler.hasOwnProperty(prop))
            return handler[prop];
        const id = crypto.randomUUID();
        Object.defineProperty(handler, prop, {
            value: id,
            enumerable: false,
            writable: false
        });
        return id;
    }
}

const finalizer = new FinalizationRegistry(finalizeInstance);
const idToInstance = new Map();
const idPool = new Array();
/** Invoked from C# to notify that imported (JS -> C#) interop instance is no longer
 *  used (eg, was garbage collected) and can be released on JavaScript side as well.
 *  @param id Unique identifier of the disposed interop instance. */
function disposeInstance(id) {
    idToInstance.delete(id);
    idPool.push(id);
}
/** Registers specified exported (C# -> JS) instance to invoke dispose on C# side
 *  when it's collected (finalized) by JavaScript runtime GC.
 *  @param instance Interop instance to register.
 *  @param id Unique identifier of the interop instance. */
function disposeOnFinalize(instance, id) {
    finalizer.register(instance, id);
}
function finalizeInstance(id) {
    exports$1.DisposeExportedInstance(id);
}

function getExports() { if (exports$1 == null) throw Error("Boot the runtime before invoking C# APIs."); return exports$1; }
function serialize(obj) { return JSON.stringify(obj); }
function deserialize(json) { const result = JSON.parse(json); if (result === null) return undefined; return result; }

/* v8 ignore start */

class Motely_JSMotelySeedExplorer {
    constructor(_id) { this._id = _id; disposeOnFinalize(this, _id); }
    createShopItemStream(ante) { MotelySeedExplorer.createShopItemStream(this._id, ante); }
    nextShopItem() { return MotelySeedExplorer.nextShopItem(this._id); }
    createShopJokerStream(ante) { MotelySeedExplorer.createShopJokerStream(this._id, ante); }
    createBuffoonPackJokerStream(ante) { MotelySeedExplorer.createBuffoonPackJokerStream(this._id, ante); }
    createJudgementJokerStream(ante) { MotelySeedExplorer.createJudgementJokerStream(this._id, ante); }
    createWraithJokerStream(ante) { MotelySeedExplorer.createWraithJokerStream(this._id, ante); }
    nextJoker() { return MotelySeedExplorer.nextJoker(this._id); }
    createSoulJokerStream(ante) { MotelySeedExplorer.createSoulJokerStream(this._id, ante); }
    createRareTagJokerStream(ante) { MotelySeedExplorer.createRareTagJokerStream(this._id, ante); }
    createUncommonTagJokerStream(ante) { MotelySeedExplorer.createUncommonTagJokerStream(this._id, ante); }
    createRiffRaffJokerStream(ante) { MotelySeedExplorer.createRiffRaffJokerStream(this._id, ante); }
    createCommonShopJokerStream(ante) { MotelySeedExplorer.createCommonShopJokerStream(this._id, ante); }
    createUncommonShopJokerStream(ante) { MotelySeedExplorer.createUncommonShopJokerStream(this._id, ante); }
    createRareShopJokerStream(ante) { MotelySeedExplorer.createRareShopJokerStream(this._id, ante); }
    nextFixedRarityJoker() { return MotelySeedExplorer.nextFixedRarityJoker(this._id); }
    createTagStream(ante) { MotelySeedExplorer.createTagStream(this._id, ante); }
    nextTag() { return MotelySeedExplorer.nextTag(this._id); }
    createVoucherStream(ante) { MotelySeedExplorer.createVoucherStream(this._id, ante); }
    nextVoucher() { return MotelySeedExplorer.nextVoucher(this._id); }
    getAnteFirstVoucher(ante) { return MotelySeedExplorer.getAnteFirstVoucher(this._id, ante); }
    createBossStream() { MotelySeedExplorer.createBossStream(this._id); }
    getBossForAnte(ante) { return MotelySeedExplorer.getBossForAnte(this._id, ante); }
    createBoosterPackStream(ante) { MotelySeedExplorer.createBoosterPackStream(this._id, ante); }
    nextBoosterPack() { return MotelySeedExplorer.nextBoosterPack(this._id); }
    createShopTarotStream(ante) { MotelySeedExplorer.createShopTarotStream(this._id, ante); }
    createArcanaPackTarotStream(ante) { MotelySeedExplorer.createArcanaPackTarotStream(this._id, ante); }
    createEmperorTarotStream(ante) { MotelySeedExplorer.createEmperorTarotStream(this._id, ante); }
    createPurpleSealTarotStream(ante) { MotelySeedExplorer.createPurpleSealTarotStream(this._id, ante); }
    nextTarot() { return MotelySeedExplorer.nextTarot(this._id); }
    createShopPlanetStream(ante) { MotelySeedExplorer.createShopPlanetStream(this._id, ante); }
    createCelestialPackPlanetStream(ante) { MotelySeedExplorer.createCelestialPackPlanetStream(this._id, ante); }
    nextPlanet() { return MotelySeedExplorer.nextPlanet(this._id); }
    createShopSpectralStream(ante) { MotelySeedExplorer.createShopSpectralStream(this._id, ante); }
    createSpectralPackSpectralStream(ante) { MotelySeedExplorer.createSpectralPackSpectralStream(this._id, ante); }
    createSixthSenseSpectralStream(ante) { MotelySeedExplorer.createSixthSenseSpectralStream(this._id, ante); }
    createSeanceSpectralStream(ante) { MotelySeedExplorer.createSeanceSpectralStream(this._id, ante); }
    nextSpectral() { return MotelySeedExplorer.nextSpectral(this._id); }
    createStandardPackCardStream(ante) { MotelySeedExplorer.createStandardPackCardStream(this._id, ante); }
    nextStandardCard() { return MotelySeedExplorer.nextStandardCard(this._id); }
    createLuckyCardMoneyStream() { MotelySeedExplorer.createLuckyCardMoneyStream(this._id); }
    nextLuckyMoney() { return MotelySeedExplorer.nextLuckyMoney(this._id); }
    createLuckyCardMultStream() { MotelySeedExplorer.createLuckyCardMultStream(this._id); }
    nextLuckyMult() { return MotelySeedExplorer.nextLuckyMult(this._id); }
    createMisprintStream() { MotelySeedExplorer.createMisprintStream(this._id); }
    nextMisprintMult() { return MotelySeedExplorer.nextMisprintMult(this._id); }
    createWheelOfFortuneStream() { MotelySeedExplorer.createWheelOfFortuneStream(this._id); }
    nextWheelOfFortune() { return MotelySeedExplorer.nextWheelOfFortune(this._id); }
    createErraticDeckStream() { MotelySeedExplorer.createErraticDeckStream(this._id); }
    nextErraticDeckCard() { return MotelySeedExplorer.nextErraticDeckCard(this._id); }
    createCavendishStream() { MotelySeedExplorer.createCavendishStream(this._id); }
    nextCavendishExtinct() { return MotelySeedExplorer.nextCavendishExtinct(this._id); }
    createGrosMichelStream() { MotelySeedExplorer.createGrosMichelStream(this._id); }
    nextGrosMichelExtinct() { return MotelySeedExplorer.nextGrosMichelExtinct(this._id); }
}

const Filters = {
    TagPosition: { "0": "Any", "1": "SmallBlind", "2": "BigBlind", "Any": 0, "SmallBlind": 1, "BigBlind": 2 },
    JamlAesthetic: { "0": "Palindrome", "1": "Psychosis", "2": "Gross", "3": "Nsfw", "4": "Funny", "5": "Balatro", "Palindrome": 0, "Psychosis": 1, "Gross": 2, "Nsfw": 3, "Funny": 4, "Balatro": 5 }
};
const Motely = {
    MotelyDeck: { "0": "Red", "1": "Blue", "2": "Yellow", "3": "Green", "4": "Black", "5": "Magic", "6": "Nebula", "7": "Ghost", "8": "Abandoned", "9": "Checkered", "10": "Zodiac", "11": "Painted", "12": "Anaglyph", "13": "Plasma", "14": "Erratic", "Red": 0, "Blue": 1, "Yellow": 2, "Green": 3, "Black": 4, "Magic": 5, "Nebula": 6, "Ghost": 7, "Abandoned": 8, "Checkered": 9, "Zodiac": 10, "Painted": 11, "Anaglyph": 12, "Plasma": 13, "Erratic": 14 },
    MotelyStake: { "0": "White", "1": "Red", "2": "Green", "3": "Black", "4": "Blue", "6": "Purple", "7": "Orange", "8": "Gold", "White": 0, "Red": 1, "Green": 2, "Black": 3, "Blue": 4, "Purple": 6, "Orange": 7, "Gold": 8 },
    MotelyJoker: { "0": "Joker", "1": "GreedyJoker", "2": "LustyJoker", "3": "WrathfulJoker", "4": "GluttonousJoker", "5": "JollyJoker", "6": "ZanyJoker", "7": "MadJoker", "8": "CrazyJoker", "9": "DrollJoker", "10": "SlyJoker", "11": "WilyJoker", "12": "CleverJoker", "13": "DeviousJoker", "14": "CraftyJoker", "15": "HalfJoker", "16": "CreditCard", "17": "Banner", "18": "MysticSummit", "19": "EightBall", "20": "Misprint", "21": "RaisedFist", "22": "ChaostheClown", "23": "ScaryFace", "24": "AbstractJoker", "25": "DelayedGratification", "26": "GrosMichel", "27": "EvenSteven", "28": "OddTodd", "29": "Scholar", "30": "BusinessCard", "31": "Supernova", "32": "RideTheBus", "33": "Egg", "34": "Runner", "35": "IceCream", "36": "Splash", "37": "BlueJoker", "38": "FacelessJoker", "39": "GreenJoker", "40": "Superposition", "41": "ToDoList", "42": "Cavendish", "43": "RedCard", "44": "SquareJoker", "45": "RiffRaff", "46": "Photograph", "47": "ReservedParking", "48": "MailInRebate", "49": "Hallucination", "50": "FortuneTeller", "51": "Juggler", "52": "Drunkard", "53": "GoldenJoker", "54": "Popcorn", "55": "WalkieTalkie", "56": "SmileyFace", "57": "GoldenTicket", "58": "Swashbuckler", "59": "HangingChad", "60": "ShootTheMoon", "1024": "JokerStencil", "1025": "FourFingers", "1026": "Mime", "1027": "CeremonialDagger", "1028": "MarbleJoker", "1029": "LoyaltyCard", "1030": "Dusk", "1031": "Fibonacci", "1032": "SteelJoker", "1033": "Hack", "1034": "Pareidolia", "1035": "SpaceJoker", "1036": "Burglar", "1037": "Blackboard", "1038": "SixthSense", "1039": "Constellation", "1040": "Hiker", "1041": "CardSharp", "1042": "Madness", "1043": "Seance", "1044": "Vampire", "1045": "Shortcut", "1046": "Hologram", "1047": "Cloud9", "1048": "Rocket", "1049": "MidasMask", "1050": "Luchador", "1051": "GiftCard", "1052": "TurtleBean", "1053": "Erosion", "1054": "ToTheMoon", "1055": "StoneJoker", "1056": "LuckyCat", "1057": "Bull", "1058": "DietCola", "1059": "TradingCard", "1060": "FlashCard", "1061": "SpareTrousers", "1062": "Ramen", "1063": "Seltzer", "1064": "Castle", "1065": "MrBones", "1066": "Acrobat", "1067": "SockAndBuskin", "1068": "Troubadour", "1069": "Certificate", "1070": "SmearedJoker", "1071": "Throwback", "1072": "RoughGem", "1073": "Bloodstone", "1074": "Arrowhead", "1075": "OnyxAgate", "1076": "GlassJoker", "1077": "Showman", "1078": "FlowerPot", "1079": "MerryAndy", "1080": "OopsAll6s", "1081": "TheIdol", "1082": "SeeingDouble", "1083": "Matador", "1084": "Satellite", "1085": "Cartomancer", "1086": "Astronomer", "1087": "Bootstraps", "2048": "DNA", "2049": "Vagabond", "2050": "Baron", "2051": "Obelisk", "2052": "BaseballCard", "2053": "AncientJoker", "2054": "Campfire", "2055": "Blueprint", "2056": "WeeJoker", "2057": "HitTheRoad", "2058": "TheDuo", "2059": "TheTrio", "2060": "TheFamily", "2061": "TheOrder", "2062": "TheTribe", "2063": "Stuntman", "2064": "InvisibleJoker", "2065": "Brainstorm", "2066": "DriversLicense", "2067": "BurntJoker", "3072": "Canio", "3073": "Triboulet", "3074": "Yorick", "3075": "Chicot", "3076": "Perkeo", "Joker": 0, "GreedyJoker": 1, "LustyJoker": 2, "WrathfulJoker": 3, "GluttonousJoker": 4, "JollyJoker": 5, "ZanyJoker": 6, "MadJoker": 7, "CrazyJoker": 8, "DrollJoker": 9, "SlyJoker": 10, "WilyJoker": 11, "CleverJoker": 12, "DeviousJoker": 13, "CraftyJoker": 14, "HalfJoker": 15, "CreditCard": 16, "Banner": 17, "MysticSummit": 18, "EightBall": 19, "Misprint": 20, "RaisedFist": 21, "ChaostheClown": 22, "ScaryFace": 23, "AbstractJoker": 24, "DelayedGratification": 25, "GrosMichel": 26, "EvenSteven": 27, "OddTodd": 28, "Scholar": 29, "BusinessCard": 30, "Supernova": 31, "RideTheBus": 32, "Egg": 33, "Runner": 34, "IceCream": 35, "Splash": 36, "BlueJoker": 37, "FacelessJoker": 38, "GreenJoker": 39, "Superposition": 40, "ToDoList": 41, "Cavendish": 42, "RedCard": 43, "SquareJoker": 44, "RiffRaff": 45, "Photograph": 46, "ReservedParking": 47, "MailInRebate": 48, "Hallucination": 49, "FortuneTeller": 50, "Juggler": 51, "Drunkard": 52, "GoldenJoker": 53, "Popcorn": 54, "WalkieTalkie": 55, "SmileyFace": 56, "GoldenTicket": 57, "Swashbuckler": 58, "HangingChad": 59, "ShootTheMoon": 60, "JokerStencil": 1024, "FourFingers": 1025, "Mime": 1026, "CeremonialDagger": 1027, "MarbleJoker": 1028, "LoyaltyCard": 1029, "Dusk": 1030, "Fibonacci": 1031, "SteelJoker": 1032, "Hack": 1033, "Pareidolia": 1034, "SpaceJoker": 1035, "Burglar": 1036, "Blackboard": 1037, "SixthSense": 1038, "Constellation": 1039, "Hiker": 1040, "CardSharp": 1041, "Madness": 1042, "Seance": 1043, "Vampire": 1044, "Shortcut": 1045, "Hologram": 1046, "Cloud9": 1047, "Rocket": 1048, "MidasMask": 1049, "Luchador": 1050, "GiftCard": 1051, "TurtleBean": 1052, "Erosion": 1053, "ToTheMoon": 1054, "StoneJoker": 1055, "LuckyCat": 1056, "Bull": 1057, "DietCola": 1058, "TradingCard": 1059, "FlashCard": 1060, "SpareTrousers": 1061, "Ramen": 1062, "Seltzer": 1063, "Castle": 1064, "MrBones": 1065, "Acrobat": 1066, "SockAndBuskin": 1067, "Troubadour": 1068, "Certificate": 1069, "SmearedJoker": 1070, "Throwback": 1071, "RoughGem": 1072, "Bloodstone": 1073, "Arrowhead": 1074, "OnyxAgate": 1075, "GlassJoker": 1076, "Showman": 1077, "FlowerPot": 1078, "MerryAndy": 1079, "OopsAll6s": 1080, "TheIdol": 1081, "SeeingDouble": 1082, "Matador": 1083, "Satellite": 1084, "Cartomancer": 1085, "Astronomer": 1086, "Bootstraps": 1087, "DNA": 2048, "Vagabond": 2049, "Baron": 2050, "Obelisk": 2051, "BaseballCard": 2052, "AncientJoker": 2053, "Campfire": 2054, "Blueprint": 2055, "WeeJoker": 2056, "HitTheRoad": 2057, "TheDuo": 2058, "TheTrio": 2059, "TheFamily": 2060, "TheOrder": 2061, "TheTribe": 2062, "Stuntman": 2063, "InvisibleJoker": 2064, "Brainstorm": 2065, "DriversLicense": 2066, "BurntJoker": 2067, "Canio": 3072, "Triboulet": 3073, "Yorick": 3074, "Chicot": 3075, "Perkeo": 3076 },
    MotelyJokerRarity: { "0": "Common", "1024": "Uncommon", "2048": "Rare", "3072": "Legendary", "Common": 0, "Uncommon": 1024, "Rare": 2048, "Legendary": 3072 },
    MotelyItemEdition: { "0": "None", "8388608": "Foil", "16777216": "Holographic", "25165824": "Polychrome", "33554432": "Negative", "None": 0, "Foil": 8388608, "Holographic": 16777216, "Polychrome": 25165824, "Negative": 33554432 },
    MotelyJokerSticker: { "0": "None", "1": "Eternal", "2": "Perishable", "3": "Rental", "None": 0, "Eternal": 1, "Perishable": 2, "Rental": 3 },
    MotelyJokerCommon: { "0": "Joker", "1": "GreedyJoker", "2": "LustyJoker", "3": "WrathfulJoker", "4": "GluttonousJoker", "5": "JollyJoker", "6": "ZanyJoker", "7": "MadJoker", "8": "CrazyJoker", "9": "DrollJoker", "10": "SlyJoker", "11": "WilyJoker", "12": "CleverJoker", "13": "DeviousJoker", "14": "CraftyJoker", "15": "HalfJoker", "16": "CreditCard", "17": "Banner", "18": "MysticSummit", "19": "EightBall", "20": "Misprint", "21": "RaisedFist", "22": "ChaostheClown", "23": "ScaryFace", "24": "AbstractJoker", "25": "DelayedGratification", "26": "GrosMichel", "27": "EvenSteven", "28": "OddTodd", "29": "Scholar", "30": "BusinessCard", "31": "Supernova", "32": "RideTheBus", "33": "Egg", "34": "Runner", "35": "IceCream", "36": "Splash", "37": "BlueJoker", "38": "FacelessJoker", "39": "GreenJoker", "40": "Superposition", "41": "ToDoList", "42": "Cavendish", "43": "RedCard", "44": "SquareJoker", "45": "RiffRaff", "46": "Photograph", "47": "ReservedParking", "48": "MailInRebate", "49": "Hallucination", "50": "FortuneTeller", "51": "Juggler", "52": "Drunkard", "53": "GoldenJoker", "54": "Popcorn", "55": "WalkieTalkie", "56": "SmileyFace", "57": "GoldenTicket", "58": "Swashbuckler", "59": "HangingChad", "60": "ShootTheMoon", "Joker": 0, "GreedyJoker": 1, "LustyJoker": 2, "WrathfulJoker": 3, "GluttonousJoker": 4, "JollyJoker": 5, "ZanyJoker": 6, "MadJoker": 7, "CrazyJoker": 8, "DrollJoker": 9, "SlyJoker": 10, "WilyJoker": 11, "CleverJoker": 12, "DeviousJoker": 13, "CraftyJoker": 14, "HalfJoker": 15, "CreditCard": 16, "Banner": 17, "MysticSummit": 18, "EightBall": 19, "Misprint": 20, "RaisedFist": 21, "ChaostheClown": 22, "ScaryFace": 23, "AbstractJoker": 24, "DelayedGratification": 25, "GrosMichel": 26, "EvenSteven": 27, "OddTodd": 28, "Scholar": 29, "BusinessCard": 30, "Supernova": 31, "RideTheBus": 32, "Egg": 33, "Runner": 34, "IceCream": 35, "Splash": 36, "BlueJoker": 37, "FacelessJoker": 38, "GreenJoker": 39, "Superposition": 40, "ToDoList": 41, "Cavendish": 42, "RedCard": 43, "SquareJoker": 44, "RiffRaff": 45, "Photograph": 46, "ReservedParking": 47, "MailInRebate": 48, "Hallucination": 49, "FortuneTeller": 50, "Juggler": 51, "Drunkard": 52, "GoldenJoker": 53, "Popcorn": 54, "WalkieTalkie": 55, "SmileyFace": 56, "GoldenTicket": 57, "Swashbuckler": 58, "HangingChad": 59, "ShootTheMoon": 60 },
    MotelyJokerUncommon: { "0": "JokerStencil", "1": "FourFingers", "2": "Mime", "3": "CeremonialDagger", "4": "MarbleJoker", "5": "LoyaltyCard", "6": "Dusk", "7": "Fibonacci", "8": "SteelJoker", "9": "Hack", "10": "Pareidolia", "11": "SpaceJoker", "12": "Burglar", "13": "Blackboard", "14": "SixthSense", "15": "Constellation", "16": "Hiker", "17": "CardSharp", "18": "Madness", "19": "Seance", "20": "Vampire", "21": "Shortcut", "22": "Hologram", "23": "Cloud9", "24": "Rocket", "25": "MidasMask", "26": "Luchador", "27": "GiftCard", "28": "TurtleBean", "29": "Erosion", "30": "ToTheMoon", "31": "StoneJoker", "32": "LuckyCat", "33": "Bull", "34": "DietCola", "35": "TradingCard", "36": "FlashCard", "37": "SpareTrousers", "38": "Ramen", "39": "Seltzer", "40": "Castle", "41": "MrBones", "42": "Acrobat", "43": "SockAndBuskin", "44": "Troubadour", "45": "Certificate", "46": "SmearedJoker", "47": "Throwback", "48": "RoughGem", "49": "Bloodstone", "50": "Arrowhead", "51": "OnyxAgate", "52": "GlassJoker", "53": "Showman", "54": "FlowerPot", "55": "MerryAndy", "56": "OopsAll6s", "57": "TheIdol", "58": "SeeingDouble", "59": "Matador", "60": "Satellite", "61": "Cartomancer", "62": "Astronomer", "63": "Bootstraps", "JokerStencil": 0, "FourFingers": 1, "Mime": 2, "CeremonialDagger": 3, "MarbleJoker": 4, "LoyaltyCard": 5, "Dusk": 6, "Fibonacci": 7, "SteelJoker": 8, "Hack": 9, "Pareidolia": 10, "SpaceJoker": 11, "Burglar": 12, "Blackboard": 13, "SixthSense": 14, "Constellation": 15, "Hiker": 16, "CardSharp": 17, "Madness": 18, "Seance": 19, "Vampire": 20, "Shortcut": 21, "Hologram": 22, "Cloud9": 23, "Rocket": 24, "MidasMask": 25, "Luchador": 26, "GiftCard": 27, "TurtleBean": 28, "Erosion": 29, "ToTheMoon": 30, "StoneJoker": 31, "LuckyCat": 32, "Bull": 33, "DietCola": 34, "TradingCard": 35, "FlashCard": 36, "SpareTrousers": 37, "Ramen": 38, "Seltzer": 39, "Castle": 40, "MrBones": 41, "Acrobat": 42, "SockAndBuskin": 43, "Troubadour": 44, "Certificate": 45, "SmearedJoker": 46, "Throwback": 47, "RoughGem": 48, "Bloodstone": 49, "Arrowhead": 50, "OnyxAgate": 51, "GlassJoker": 52, "Showman": 53, "FlowerPot": 54, "MerryAndy": 55, "OopsAll6s": 56, "TheIdol": 57, "SeeingDouble": 58, "Matador": 59, "Satellite": 60, "Cartomancer": 61, "Astronomer": 62, "Bootstraps": 63 },
    MotelyJokerRare: { "0": "DNA", "1": "Vagabond", "2": "Baron", "3": "Obelisk", "4": "BaseballCard", "5": "AncientJoker", "6": "Campfire", "7": "Blueprint", "8": "WeeJoker", "9": "HitTheRoad", "10": "TheDuo", "11": "TheTrio", "12": "TheFamily", "13": "TheOrder", "14": "TheTribe", "15": "Stuntman", "16": "InvisibleJoker", "17": "Brainstorm", "18": "DriversLicense", "19": "BurntJoker", "DNA": 0, "Vagabond": 1, "Baron": 2, "Obelisk": 3, "BaseballCard": 4, "AncientJoker": 5, "Campfire": 6, "Blueprint": 7, "WeeJoker": 8, "HitTheRoad": 9, "TheDuo": 10, "TheTrio": 11, "TheFamily": 12, "TheOrder": 13, "TheTribe": 14, "Stuntman": 15, "InvisibleJoker": 16, "Brainstorm": 17, "DriversLicense": 18, "BurntJoker": 19 },
    MotelyVoucher: { "0": "Overstock", "1": "OverstockPlus", "2": "ClearanceSale", "3": "Liquidation", "4": "Hone", "5": "GlowUp", "6": "RerollSurplus", "7": "RerollGlut", "8": "CrystalBall", "9": "OmenGlobe", "10": "Telescope", "11": "Observatory", "12": "Grabber", "13": "NachoTong", "14": "Wasteful", "15": "Recyclomancy", "16": "TarotMerchant", "17": "TarotTycoon", "18": "PlanetMerchant", "19": "PlanetTycoon", "20": "SeedMoney", "21": "MoneyTree", "22": "Blank", "23": "Antimatter", "24": "MagicTrick", "25": "Illusion", "26": "Hieroglyph", "27": "Petroglyph", "28": "DirectorsCut", "29": "Retcon", "30": "PaintBrush", "31": "Palette", "Overstock": 0, "OverstockPlus": 1, "ClearanceSale": 2, "Liquidation": 3, "Hone": 4, "GlowUp": 5, "RerollSurplus": 6, "RerollGlut": 7, "CrystalBall": 8, "OmenGlobe": 9, "Telescope": 10, "Observatory": 11, "Grabber": 12, "NachoTong": 13, "Wasteful": 14, "Recyclomancy": 15, "TarotMerchant": 16, "TarotTycoon": 17, "PlanetMerchant": 18, "PlanetTycoon": 19, "SeedMoney": 20, "MoneyTree": 21, "Blank": 22, "Antimatter": 23, "MagicTrick": 24, "Illusion": 25, "Hieroglyph": 26, "Petroglyph": 27, "DirectorsCut": 28, "Retcon": 29, "PaintBrush": 30, "Palette": 31 },
    MotelyTarotCard: { "0": "TheFool", "1": "TheMagician", "2": "TheHighPriestess", "3": "TheEmpress", "4": "TheEmperor", "5": "TheHierophant", "6": "TheLovers", "7": "TheChariot", "8": "Justice", "9": "TheHermit", "10": "TheWheelOfFortune", "11": "Strength", "12": "TheHangedMan", "13": "Death", "14": "Temperance", "15": "TheDevil", "16": "TheTower", "17": "TheStar", "18": "TheMoon", "19": "TheSun", "20": "Judgement", "21": "TheWorld", "TheFool": 0, "TheMagician": 1, "TheHighPriestess": 2, "TheEmpress": 3, "TheEmperor": 4, "TheHierophant": 5, "TheLovers": 6, "TheChariot": 7, "Justice": 8, "TheHermit": 9, "TheWheelOfFortune": 10, "Strength": 11, "TheHangedMan": 12, "Death": 13, "Temperance": 14, "TheDevil": 15, "TheTower": 16, "TheStar": 17, "TheMoon": 18, "TheSun": 19, "Judgement": 20, "TheWorld": 21 },
    MotelySpectralCard: { "0": "Familiar", "1": "Grim", "2": "Incantation", "3": "Talisman", "4": "Aura", "5": "Wraith", "6": "Sigil", "7": "Ouija", "8": "Ectoplasm", "9": "Immolate", "10": "Ankh", "11": "DejaVu", "12": "Hex", "13": "Trance", "14": "Medium", "15": "Cryptid", "16": "TheSoul", "17": "BlackHole", "Familiar": 0, "Grim": 1, "Incantation": 2, "Talisman": 3, "Aura": 4, "Wraith": 5, "Sigil": 6, "Ouija": 7, "Ectoplasm": 8, "Immolate": 9, "Ankh": 10, "DejaVu": 11, "Hex": 12, "Trance": 13, "Medium": 14, "Cryptid": 15, "TheSoul": 16, "BlackHole": 17 },
    MotelyPlanetCard: { "0": "Mercury", "1": "Venus", "2": "Earth", "3": "Mars", "4": "Jupiter", "5": "Saturn", "6": "Uranus", "7": "Neptune", "8": "Pluto", "9": "PlanetX", "10": "Ceres", "11": "Eris", "Mercury": 0, "Venus": 1, "Earth": 2, "Mars": 3, "Jupiter": 4, "Saturn": 5, "Uranus": 6, "Neptune": 7, "Pluto": 8, "PlanetX": 9, "Ceres": 10, "Eris": 11 },
    MotelyPlayingCardRank: { "0": "Two", "1": "Three", "2": "Four", "3": "Five", "4": "Six", "5": "Seven", "6": "Eight", "7": "Nine", "8": "Ten", "9": "Jack", "10": "Queen", "11": "King", "12": "Ace", "Two": 0, "Three": 1, "Four": 2, "Five": 3, "Six": 4, "Seven": 5, "Eight": 6, "Nine": 7, "Ten": 8, "Jack": 9, "Queen": 10, "King": 11, "Ace": 12 },
    MotelyPlayingCardSuit: { "0": "Clubs", "16": "Diamonds", "32": "Hearts", "48": "Spades", "Clubs": 0, "Diamonds": 16, "Hearts": 32, "Spades": 48 },
    MotelyItemEnhancement: { "0": "None", "524288": "Bonus", "1048576": "Mult", "1572864": "Wild", "2097152": "Glass", "2621440": "Steel", "3145728": "Stone", "3670016": "Gold", "4194304": "Lucky", "None": 0, "Bonus": 524288, "Mult": 1048576, "Wild": 1572864, "Glass": 2097152, "Steel": 2621440, "Stone": 3145728, "Gold": 3670016, "Lucky": 4194304 },
    MotelyItemSeal: { "0": "None", "65536": "Gold", "131072": "Red", "196608": "Blue", "262144": "Purple", "None": 0, "Gold": 65536, "Red": 131072, "Blue": 196608, "Purple": 262144 },
    MotelyBossBlind: { "-2147483648": "", "-2147483647": "", "-2147483646": "", "-2147483645": "", "-2147483644": "", "536870917": "TheArm", "6": "", "805306375": "TheEye", "536870920": "TheFish", "536870921": "TheFlint", "10": "TheGoad", "11": "TheHead", "12": "TheHook", "536870925": "TheHouse", "14": "TheManacle", "536870927": "TheMark", "536870928": "TheMouth", "536870929": "TheNeedle", "1610612754": "TheOx", "19": "ThePillar", "1073741844": "ThePlant", "21": "ThePsychic", "1342177302": "TheSerpent", "805306391": "TheTooth", "536870936": "TheWall", "536870937": "TheWater", "536870938": "TheWheel", "27": "TheWindow", "": -2147483648, "": -2147483647, "": -2147483646, "": -2147483645, "": -2147483644, "TheArm": 536870917, "": 6, "TheEye": 805306375, "TheFish": 536870920, "TheFlint": 536870921, "TheGoad": 10, "TheHead": 11, "TheHook": 12, "TheHouse": 536870925, "TheManacle": 14, "TheMark": 536870927, "TheMouth": 536870928, "TheNeedle": 536870929, "TheOx": 1610612754, "ThePillar": 19, "ThePlant": 1073741844, "ThePsychic": 21, "TheSerpent": 1342177302, "TheTooth": 805306391, "TheWall": 536870936, "TheWater": 536870937, "TheWheel": 536870938, "TheWindow": 27 },
    MotelyTag: { "0": "UncommonTag", "1": "RareTag", "2": "NegativeTag", "3": "FoilTag", "4": "HolographicTag", "5": "PolychromeTag", "6": "InvestmentTag", "7": "VoucherTag", "8": "BossTag", "9": "StandardTag", "10": "CharmTag", "11": "MeteorTag", "12": "BuffoonTag", "13": "HandyTag", "14": "GarbageTag", "15": "EtherealTag", "16": "CouponTag", "17": "DoubleTag", "18": "JuggleTag", "19": "D6Tag", "20": "TopupTag", "21": "SpeedTag", "22": "OrbitalTag", "23": "EconomyTag", "UncommonTag": 0, "RareTag": 1, "NegativeTag": 2, "FoilTag": 3, "HolographicTag": 4, "PolychromeTag": 5, "InvestmentTag": 6, "VoucherTag": 7, "BossTag": 8, "StandardTag": 9, "CharmTag": 10, "MeteorTag": 11, "BuffoonTag": 12, "HandyTag": 13, "GarbageTag": 14, "EtherealTag": 15, "CouponTag": 16, "DoubleTag": 17, "JuggleTag": 18, "D6Tag": 19, "TopupTag": 20, "SpeedTag": 21, "OrbitalTag": 22, "EconomyTag": 23 }
};
const MotelyProgram = {
    getVersion: () => getExports().Bootsharp_Generated_Exports_Motely_BrowserWasm_JSMotelyProgram_GetVersion(),
    loadJaml: (jaml) => deserialize(getExports().Bootsharp_Generated_Exports_Motely_BrowserWasm_JSMotelyProgram_LoadJaml(jaml)),
    compileJummy: (jummy) => deserialize(getExports().Bootsharp_Generated_Exports_Motely_BrowserWasm_JSMotelyProgram_CompileJummy(jummy)),
    createSeedExplorer: (seed, deck, stake) => new Motely_JSMotelySeedExplorer(getExports().Bootsharp_Generated_Exports_Motely_BrowserWasm_JSMotelyProgram_CreateSeedExplorer(seed, serialize(deck), serialize(stake))),
    stopSearch: () => getExports().Bootsharp_Generated_Exports_Motely_BrowserWasm_JSMotelyProgram_StopSearch(),
    startConfiguredSearch: (jaml, batchCharCount, startBatch, endBatch) => getExports().Bootsharp_Generated_Exports_Motely_BrowserWasm_JSMotelyProgram_StartConfiguredSearch(serialize(jaml), batchCharCount, startBatch, endBatch),
    startSequentialSearch: (jaml, batchCharCount, startBatch, endBatch) => getExports().Bootsharp_Generated_Exports_Motely_BrowserWasm_JSMotelyProgram_StartSequentialSearch(serialize(jaml), batchCharCount, startBatch, endBatch),
    startRandomSearch: (jaml, randomSeedCount, batchCharCount) => getExports().Bootsharp_Generated_Exports_Motely_BrowserWasm_JSMotelyProgram_StartRandomSearch(serialize(jaml), randomSeedCount, batchCharCount),
    startAestheticSearch: (jaml, aesthetic, batchCharCount) => getExports().Bootsharp_Generated_Exports_Motely_BrowserWasm_JSMotelyProgram_StartAestheticSearch(serialize(jaml), aesthetic, batchCharCount),
    startKeywordSearch: (jaml, keywordsCsv, paddingChars, batchCharCount) => getExports().Bootsharp_Generated_Exports_Motely_BrowserWasm_JSMotelyProgram_StartKeywordSearch(serialize(jaml), keywordsCsv, paddingChars, batchCharCount),
    startSeedListSearch: (jaml, seedsCsv, threadCount) => getExports().Bootsharp_Generated_Exports_Motely_BrowserWasm_JSMotelyProgram_StartSeedListSearch(serialize(jaml), seedsCsv, threadCount)
};
const MotelySeedExplorer = {
    createShopItemStream: (ante) => getExports().Bootsharp_Generated_Exports_Motely_JSMotelySeedExplorer_CreateShopItemStream(ante),
    nextShopItem: (_id) => getExports().Bootsharp_Generated_Exports_Motely_JSMotelySeedExplorer_NextShopItem(_id),
    createShopJokerStream: (ante) => getExports().Bootsharp_Generated_Exports_Motely_JSMotelySeedExplorer_CreateShopJokerStream(ante),
    createBuffoonPackJokerStream: (ante) => getExports().Bootsharp_Generated_Exports_Motely_JSMotelySeedExplorer_CreateBuffoonPackJokerStream(ante),
    createJudgementJokerStream: (ante) => getExports().Bootsharp_Generated_Exports_Motely_JSMotelySeedExplorer_CreateJudgementJokerStream(ante),
    createWraithJokerStream: (ante) => getExports().Bootsharp_Generated_Exports_Motely_JSMotelySeedExplorer_CreateWraithJokerStream(ante),
    nextJoker: (_id) => getExports().Bootsharp_Generated_Exports_Motely_JSMotelySeedExplorer_NextJoker(_id),
    createSoulJokerStream: (ante) => getExports().Bootsharp_Generated_Exports_Motely_JSMotelySeedExplorer_CreateSoulJokerStream(ante),
    createRareTagJokerStream: (ante) => getExports().Bootsharp_Generated_Exports_Motely_JSMotelySeedExplorer_CreateRareTagJokerStream(ante),
    createUncommonTagJokerStream: (ante) => getExports().Bootsharp_Generated_Exports_Motely_JSMotelySeedExplorer_CreateUncommonTagJokerStream(ante),
    createRiffRaffJokerStream: (ante) => getExports().Bootsharp_Generated_Exports_Motely_JSMotelySeedExplorer_CreateRiffRaffJokerStream(ante),
    createCommonShopJokerStream: (ante) => getExports().Bootsharp_Generated_Exports_Motely_JSMotelySeedExplorer_CreateCommonShopJokerStream(ante),
    createUncommonShopJokerStream: (ante) => getExports().Bootsharp_Generated_Exports_Motely_JSMotelySeedExplorer_CreateUncommonShopJokerStream(ante),
    createRareShopJokerStream: (ante) => getExports().Bootsharp_Generated_Exports_Motely_JSMotelySeedExplorer_CreateRareShopJokerStream(ante),
    nextFixedRarityJoker: (_id) => getExports().Bootsharp_Generated_Exports_Motely_JSMotelySeedExplorer_NextFixedRarityJoker(_id),
    createTagStream: (ante) => getExports().Bootsharp_Generated_Exports_Motely_JSMotelySeedExplorer_CreateTagStream(ante),
    nextTag: (_id) => getExports().Bootsharp_Generated_Exports_Motely_JSMotelySeedExplorer_NextTag(_id),
    createVoucherStream: (ante) => getExports().Bootsharp_Generated_Exports_Motely_JSMotelySeedExplorer_CreateVoucherStream(ante),
    nextVoucher: (_id) => getExports().Bootsharp_Generated_Exports_Motely_JSMotelySeedExplorer_NextVoucher(_id),
    getAnteFirstVoucher: (ante) => getExports().Bootsharp_Generated_Exports_Motely_JSMotelySeedExplorer_GetAnteFirstVoucher(ante),
    createBossStream: (_id) => getExports().Bootsharp_Generated_Exports_Motely_JSMotelySeedExplorer_CreateBossStream(_id),
    getBossForAnte: (ante) => getExports().Bootsharp_Generated_Exports_Motely_JSMotelySeedExplorer_GetBossForAnte(ante),
    createBoosterPackStream: (ante) => getExports().Bootsharp_Generated_Exports_Motely_JSMotelySeedExplorer_CreateBoosterPackStream(ante),
    nextBoosterPack: (_id) => getExports().Bootsharp_Generated_Exports_Motely_JSMotelySeedExplorer_NextBoosterPack(_id),
    createShopTarotStream: (ante) => getExports().Bootsharp_Generated_Exports_Motely_JSMotelySeedExplorer_CreateShopTarotStream(ante),
    createArcanaPackTarotStream: (ante) => getExports().Bootsharp_Generated_Exports_Motely_JSMotelySeedExplorer_CreateArcanaPackTarotStream(ante),
    createEmperorTarotStream: (ante) => getExports().Bootsharp_Generated_Exports_Motely_JSMotelySeedExplorer_CreateEmperorTarotStream(ante),
    createPurpleSealTarotStream: (ante) => getExports().Bootsharp_Generated_Exports_Motely_JSMotelySeedExplorer_CreatePurpleSealTarotStream(ante),
    nextTarot: (_id) => getExports().Bootsharp_Generated_Exports_Motely_JSMotelySeedExplorer_NextTarot(_id),
    createShopPlanetStream: (ante) => getExports().Bootsharp_Generated_Exports_Motely_JSMotelySeedExplorer_CreateShopPlanetStream(ante),
    createCelestialPackPlanetStream: (ante) => getExports().Bootsharp_Generated_Exports_Motely_JSMotelySeedExplorer_CreateCelestialPackPlanetStream(ante),
    nextPlanet: (_id) => getExports().Bootsharp_Generated_Exports_Motely_JSMotelySeedExplorer_NextPlanet(_id),
    createShopSpectralStream: (ante) => getExports().Bootsharp_Generated_Exports_Motely_JSMotelySeedExplorer_CreateShopSpectralStream(ante),
    createSpectralPackSpectralStream: (ante) => getExports().Bootsharp_Generated_Exports_Motely_JSMotelySeedExplorer_CreateSpectralPackSpectralStream(ante),
    createSixthSenseSpectralStream: (ante) => getExports().Bootsharp_Generated_Exports_Motely_JSMotelySeedExplorer_CreateSixthSenseSpectralStream(ante),
    createSeanceSpectralStream: (ante) => getExports().Bootsharp_Generated_Exports_Motely_JSMotelySeedExplorer_CreateSeanceSpectralStream(ante),
    nextSpectral: (_id) => getExports().Bootsharp_Generated_Exports_Motely_JSMotelySeedExplorer_NextSpectral(_id),
    createStandardPackCardStream: (ante) => getExports().Bootsharp_Generated_Exports_Motely_JSMotelySeedExplorer_CreateStandardPackCardStream(ante),
    nextStandardCard: (_id) => getExports().Bootsharp_Generated_Exports_Motely_JSMotelySeedExplorer_NextStandardCard(_id),
    createLuckyCardMoneyStream: (_id) => getExports().Bootsharp_Generated_Exports_Motely_JSMotelySeedExplorer_CreateLuckyCardMoneyStream(_id),
    nextLuckyMoney: (_id) => getExports().Bootsharp_Generated_Exports_Motely_JSMotelySeedExplorer_NextLuckyMoney(_id),
    createLuckyCardMultStream: (_id) => getExports().Bootsharp_Generated_Exports_Motely_JSMotelySeedExplorer_CreateLuckyCardMultStream(_id),
    nextLuckyMult: (_id) => getExports().Bootsharp_Generated_Exports_Motely_JSMotelySeedExplorer_NextLuckyMult(_id),
    createMisprintStream: (_id) => getExports().Bootsharp_Generated_Exports_Motely_JSMotelySeedExplorer_CreateMisprintStream(_id),
    nextMisprintMult: (_id) => getExports().Bootsharp_Generated_Exports_Motely_JSMotelySeedExplorer_NextMisprintMult(_id),
    createWheelOfFortuneStream: (_id) => getExports().Bootsharp_Generated_Exports_Motely_JSMotelySeedExplorer_CreateWheelOfFortuneStream(_id),
    nextWheelOfFortune: (_id) => getExports().Bootsharp_Generated_Exports_Motely_JSMotelySeedExplorer_NextWheelOfFortune(_id),
    createErraticDeckStream: (_id) => getExports().Bootsharp_Generated_Exports_Motely_JSMotelySeedExplorer_CreateErraticDeckStream(_id),
    nextErraticDeckCard: (_id) => getExports().Bootsharp_Generated_Exports_Motely_JSMotelySeedExplorer_NextErraticDeckCard(_id),
    createCavendishStream: (_id) => getExports().Bootsharp_Generated_Exports_Motely_JSMotelySeedExplorer_CreateCavendishStream(_id),
    nextCavendishExtinct: (_id) => getExports().Bootsharp_Generated_Exports_Motely_JSMotelySeedExplorer_NextCavendishExtinct(_id),
    createGrosMichelStream: (_id) => getExports().Bootsharp_Generated_Exports_Motely_JSMotelySeedExplorer_CreateGrosMichelStream(_id),
    nextGrosMichelExtinct: (_id) => getExports().Bootsharp_Generated_Exports_Motely_JSMotelySeedExplorer_NextGrosMichelExtinct(_id),
    createShopItemStream: (_id, ante) => getExports().Bootsharp_Generated_Exports_Motely_JSMotelySeedExplorer_CreateShopItemStream(_id, ante),
    nextShopItem: (_id) => getExports().Bootsharp_Generated_Exports_Motely_JSMotelySeedExplorer_NextShopItem(_id),
    createShopJokerStream: (_id, ante) => getExports().Bootsharp_Generated_Exports_Motely_JSMotelySeedExplorer_CreateShopJokerStream(_id, ante),
    createBuffoonPackJokerStream: (_id, ante) => getExports().Bootsharp_Generated_Exports_Motely_JSMotelySeedExplorer_CreateBuffoonPackJokerStream(_id, ante),
    createJudgementJokerStream: (_id, ante) => getExports().Bootsharp_Generated_Exports_Motely_JSMotelySeedExplorer_CreateJudgementJokerStream(_id, ante),
    createWraithJokerStream: (_id, ante) => getExports().Bootsharp_Generated_Exports_Motely_JSMotelySeedExplorer_CreateWraithJokerStream(_id, ante),
    nextJoker: (_id) => getExports().Bootsharp_Generated_Exports_Motely_JSMotelySeedExplorer_NextJoker(_id),
    createSoulJokerStream: (_id, ante) => getExports().Bootsharp_Generated_Exports_Motely_JSMotelySeedExplorer_CreateSoulJokerStream(_id, ante),
    createRareTagJokerStream: (_id, ante) => getExports().Bootsharp_Generated_Exports_Motely_JSMotelySeedExplorer_CreateRareTagJokerStream(_id, ante),
    createUncommonTagJokerStream: (_id, ante) => getExports().Bootsharp_Generated_Exports_Motely_JSMotelySeedExplorer_CreateUncommonTagJokerStream(_id, ante),
    createRiffRaffJokerStream: (_id, ante) => getExports().Bootsharp_Generated_Exports_Motely_JSMotelySeedExplorer_CreateRiffRaffJokerStream(_id, ante),
    createCommonShopJokerStream: (_id, ante) => getExports().Bootsharp_Generated_Exports_Motely_JSMotelySeedExplorer_CreateCommonShopJokerStream(_id, ante),
    createUncommonShopJokerStream: (_id, ante) => getExports().Bootsharp_Generated_Exports_Motely_JSMotelySeedExplorer_CreateUncommonShopJokerStream(_id, ante),
    createRareShopJokerStream: (_id, ante) => getExports().Bootsharp_Generated_Exports_Motely_JSMotelySeedExplorer_CreateRareShopJokerStream(_id, ante),
    nextFixedRarityJoker: (_id) => getExports().Bootsharp_Generated_Exports_Motely_JSMotelySeedExplorer_NextFixedRarityJoker(_id),
    createTagStream: (_id, ante) => getExports().Bootsharp_Generated_Exports_Motely_JSMotelySeedExplorer_CreateTagStream(_id, ante),
    nextTag: (_id) => getExports().Bootsharp_Generated_Exports_Motely_JSMotelySeedExplorer_NextTag(_id),
    createVoucherStream: (_id, ante) => getExports().Bootsharp_Generated_Exports_Motely_JSMotelySeedExplorer_CreateVoucherStream(_id, ante),
    nextVoucher: (_id) => getExports().Bootsharp_Generated_Exports_Motely_JSMotelySeedExplorer_NextVoucher(_id),
    getAnteFirstVoucher: (_id, ante) => getExports().Bootsharp_Generated_Exports_Motely_JSMotelySeedExplorer_GetAnteFirstVoucher(_id, ante),
    createBossStream: (_id) => getExports().Bootsharp_Generated_Exports_Motely_JSMotelySeedExplorer_CreateBossStream(_id),
    getBossForAnte: (_id, ante) => getExports().Bootsharp_Generated_Exports_Motely_JSMotelySeedExplorer_GetBossForAnte(_id, ante),
    createBoosterPackStream: (_id, ante) => getExports().Bootsharp_Generated_Exports_Motely_JSMotelySeedExplorer_CreateBoosterPackStream(_id, ante),
    nextBoosterPack: (_id) => getExports().Bootsharp_Generated_Exports_Motely_JSMotelySeedExplorer_NextBoosterPack(_id),
    createShopTarotStream: (_id, ante) => getExports().Bootsharp_Generated_Exports_Motely_JSMotelySeedExplorer_CreateShopTarotStream(_id, ante),
    createArcanaPackTarotStream: (_id, ante) => getExports().Bootsharp_Generated_Exports_Motely_JSMotelySeedExplorer_CreateArcanaPackTarotStream(_id, ante),
    createEmperorTarotStream: (_id, ante) => getExports().Bootsharp_Generated_Exports_Motely_JSMotelySeedExplorer_CreateEmperorTarotStream(_id, ante),
    createPurpleSealTarotStream: (_id, ante) => getExports().Bootsharp_Generated_Exports_Motely_JSMotelySeedExplorer_CreatePurpleSealTarotStream(_id, ante),
    nextTarot: (_id) => getExports().Bootsharp_Generated_Exports_Motely_JSMotelySeedExplorer_NextTarot(_id),
    createShopPlanetStream: (_id, ante) => getExports().Bootsharp_Generated_Exports_Motely_JSMotelySeedExplorer_CreateShopPlanetStream(_id, ante),
    createCelestialPackPlanetStream: (_id, ante) => getExports().Bootsharp_Generated_Exports_Motely_JSMotelySeedExplorer_CreateCelestialPackPlanetStream(_id, ante),
    nextPlanet: (_id) => getExports().Bootsharp_Generated_Exports_Motely_JSMotelySeedExplorer_NextPlanet(_id),
    createShopSpectralStream: (_id, ante) => getExports().Bootsharp_Generated_Exports_Motely_JSMotelySeedExplorer_CreateShopSpectralStream(_id, ante),
    createSpectralPackSpectralStream: (_id, ante) => getExports().Bootsharp_Generated_Exports_Motely_JSMotelySeedExplorer_CreateSpectralPackSpectralStream(_id, ante),
    createSixthSenseSpectralStream: (_id, ante) => getExports().Bootsharp_Generated_Exports_Motely_JSMotelySeedExplorer_CreateSixthSenseSpectralStream(_id, ante),
    createSeanceSpectralStream: (_id, ante) => getExports().Bootsharp_Generated_Exports_Motely_JSMotelySeedExplorer_CreateSeanceSpectralStream(_id, ante),
    nextSpectral: (_id) => getExports().Bootsharp_Generated_Exports_Motely_JSMotelySeedExplorer_NextSpectral(_id),
    createStandardPackCardStream: (_id, ante) => getExports().Bootsharp_Generated_Exports_Motely_JSMotelySeedExplorer_CreateStandardPackCardStream(_id, ante),
    nextStandardCard: (_id) => getExports().Bootsharp_Generated_Exports_Motely_JSMotelySeedExplorer_NextStandardCard(_id),
    createLuckyCardMoneyStream: (_id) => getExports().Bootsharp_Generated_Exports_Motely_JSMotelySeedExplorer_CreateLuckyCardMoneyStream(_id),
    nextLuckyMoney: (_id) => getExports().Bootsharp_Generated_Exports_Motely_JSMotelySeedExplorer_NextLuckyMoney(_id),
    createLuckyCardMultStream: (_id) => getExports().Bootsharp_Generated_Exports_Motely_JSMotelySeedExplorer_CreateLuckyCardMultStream(_id),
    nextLuckyMult: (_id) => getExports().Bootsharp_Generated_Exports_Motely_JSMotelySeedExplorer_NextLuckyMult(_id),
    createMisprintStream: (_id) => getExports().Bootsharp_Generated_Exports_Motely_JSMotelySeedExplorer_CreateMisprintStream(_id),
    nextMisprintMult: (_id) => getExports().Bootsharp_Generated_Exports_Motely_JSMotelySeedExplorer_NextMisprintMult(_id),
    createWheelOfFortuneStream: (_id) => getExports().Bootsharp_Generated_Exports_Motely_JSMotelySeedExplorer_CreateWheelOfFortuneStream(_id),
    nextWheelOfFortune: (_id) => getExports().Bootsharp_Generated_Exports_Motely_JSMotelySeedExplorer_NextWheelOfFortune(_id),
    createErraticDeckStream: (_id) => getExports().Bootsharp_Generated_Exports_Motely_JSMotelySeedExplorer_CreateErraticDeckStream(_id),
    nextErraticDeckCard: (_id) => getExports().Bootsharp_Generated_Exports_Motely_JSMotelySeedExplorer_NextErraticDeckCard(_id),
    createCavendishStream: (_id) => getExports().Bootsharp_Generated_Exports_Motely_JSMotelySeedExplorer_CreateCavendishStream(_id),
    nextCavendishExtinct: (_id) => getExports().Bootsharp_Generated_Exports_Motely_JSMotelySeedExplorer_NextCavendishExtinct(_id),
    createGrosMichelStream: (_id) => getExports().Bootsharp_Generated_Exports_Motely_JSMotelySeedExplorer_CreateGrosMichelStream(_id),
    nextGrosMichelExtinct: (_id) => getExports().Bootsharp_Generated_Exports_Motely_JSMotelySeedExplorer_NextGrosMichelExtinct(_id)
};
const SearchEvents = {
    onProgress: new Event(),
    onProgressSerialized: (seedsSearched, matchingSeeds, elapsedMs) => SearchEvents.onProgress.broadcast(seedsSearched, matchingSeeds, elapsedMs),
    onResult: new Event(),
    onResultSerialized: (seed, score, tallyColumns) => SearchEvents.onResult.broadcast(seed, score, tallyColumns),
    onComplete: new Event(),
    onCompleteSerialized: (status, seedsSearched, matchingSeeds) => SearchEvents.onComplete.broadcast(status, seedsSearched, matchingSeeds)
};

var bindings = /*#__PURE__*/Object.freeze({
    __proto__: null,
    Filters: Filters,
    Motely: Motely,
    MotelyProgram: MotelyProgram,
    MotelySeedExplorer: MotelySeedExplorer,
    SearchEvents: SearchEvents
});

function bindImports(runtime) {
    runtime.setModuleImports("Bootsharp", {
        ...bindings,
        disposeInstance,
        disposeOnFinalize
    });
}

/** Lifecycle status of the runtime module. */
var BootStatus;
(function (BootStatus) {
    /** Ready to boot. */
    BootStatus[BootStatus["Standby"] = 0] = "Standby";
    /** Async boot process is in progress. */
    BootStatus[BootStatus["Booting"] = 1] = "Booting";
    /** Booted and ready for interop. */
    BootStatus[BootStatus["Booted"] = 2] = "Booted";
})(BootStatus || (BootStatus = {}));
let status = BootStatus.Standby;
let main;
/** Returns current runtime module lifecycle state. */
function getStatus() {
    return status;
}
/** Initializes .NET runtime and binds C# APIs.
 *  @param options Specify to configure the boot process.
 *  @return Promise that resolves into .NET runtime instance. */
async function boot(options) {
    if (status === BootStatus.Booted)
        throw Error("Failed to boot .NET runtime: already booted.");
    if (status === BootStatus.Booting)
        throw Error("Failed to boot .NET runtime: already booting.");
    status = BootStatus.Booting;
    main = await getMain(options?.root);
    const runtime = await createRuntime(main, options);
    status = BootStatus.Booted;
    return runtime;
}
/** Terminates .NET runtime and removes WASM module from memory.
 *  @param code Exit code; will use 0 (normal exit) by default.
 *  @param reason Exit reason description (optional). */
async function exit(code, reason) {
    if (status !== BootStatus.Booted)
        throw Error("Failed to exit .NET runtime: not booted.");
    try {
        main?.exit(code ?? 0, reason);
    }
    catch { }
    finally {
        status = BootStatus.Standby;
    }
}
async function createRuntime(main, opt) {
    const cfg = opt?.config ?? await buildConfig(opt?.resources ?? resources, opt?.root);
    const runtime = await opt?.create?.(cfg) || await main.dotnet.withConfig(cfg).create();
    if (opt?.import)
        await opt.import(runtime);
    else
        bindImports(runtime);
    if (opt?.run)
        await opt.run(runtime);
    else
        await runtime.runMain(cfg.mainAssemblyName, []);
    if (opt?.export)
        await opt.export(runtime);
    else
        await bindExports(runtime, cfg.mainAssemblyName);
    return runtime;
}

var index = {
    boot,
    exit,
    getStatus,
    BootStatus,
    resources,
    /** .NET internal modules and associated utilities. */
    dotnet: { getMain, getNative, getRuntime, buildConfig }
};

const embedded$2 = false;
const mt$2 = false;

var dotnet_g = /*#__PURE__*/Object.freeze({
    __proto__: null,
    embedded: embedded$2,
    mt: mt$2
});

const embedded$1 = false;
const mt$1 = false;

var dotnet_native_g = /*#__PURE__*/Object.freeze({
    __proto__: null,
    embedded: embedded$1,
    mt: mt$1
});

const embedded = false;
const mt = false;

var dotnet_runtime_g = /*#__PURE__*/Object.freeze({
    __proto__: null,
    embedded: embedded,
    mt: mt
});

export { Event, Filters, Motely, MotelyProgram, MotelySeedExplorer, SearchEvents, index as default };
