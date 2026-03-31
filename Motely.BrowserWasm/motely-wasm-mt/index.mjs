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
        { name: "Bootsharp.Common.wasm", content: undefined },
        { name: "Bootsharp.Inject.wasm", content: undefined },
        { name: "Microsoft.Extensions.DependencyInjection.Abstractions.wasm", content: undefined },
        { name: "Microsoft.Extensions.DependencyInjection.wasm", content: undefined },
        { name: "Motely.BrowserWasm.wasm", content: undefined },
        { name: "Motely.wasm", content: undefined },
        { name: "System.Collections.Concurrent.wasm", content: undefined },
        { name: "System.Collections.Immutable.wasm", content: undefined },
        { name: "System.Collections.wasm", content: undefined },
        { name: "System.ComponentModel.Primitives.wasm", content: undefined },
        { name: "System.ComponentModel.wasm", content: undefined },
        { name: "System.Console.wasm", content: undefined },
        { name: "System.IO.Pipelines.wasm", content: undefined },
        { name: "System.Linq.wasm", content: undefined },
        { name: "System.Memory.wasm", content: undefined },
        { name: "System.ObjectModel.wasm", content: undefined },
        { name: "System.Private.CoreLib.wasm", content: undefined },
        { name: "System.Private.Uri.wasm", content: undefined },
        { name: "System.Runtime.InteropServices.JavaScript.wasm", content: undefined },
        { name: "System.Text.Encodings.Web.wasm", content: undefined },
        { name: "System.Text.Json.wasm", content: undefined },
        { name: "System.Text.RegularExpressions.wasm", content: undefined },
        { name: "System.Threading.Channels.wasm", content: undefined },
        { name: "YamlDotNet.wasm", content: undefined }
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


const Motely = {
    MotelyDeck: { "0": "Red", "1": "Blue", "2": "Yellow", "3": "Green", "4": "Black", "5": "Magic", "6": "Nebula", "7": "Ghost", "8": "Abandoned", "9": "Checkered", "10": "Zodiac", "11": "Painted", "12": "Anaglyph", "13": "Plasma", "14": "Erratic", "Red": 0, "Blue": 1, "Yellow": 2, "Green": 3, "Black": 4, "Magic": 5, "Nebula": 6, "Ghost": 7, "Abandoned": 8, "Checkered": 9, "Zodiac": 10, "Painted": 11, "Anaglyph": 12, "Plasma": 13, "Erratic": 14 },
    MotelyStake: { "0": "White", "1": "Red", "2": "Green", "3": "Black", "4": "Blue", "6": "Purple", "7": "Orange", "8": "Gold", "White": 0, "Red": 1, "Green": 2, "Black": 3, "Blue": 4, "Purple": 6, "Orange": 7, "Gold": 8 },
    MotelyBossBlind: { "-2147483648": "", "-2147483647": "", "-2147483646": "", "-2147483645": "", "-2147483644": "", "536870917": "TheArm", "6": "", "805306375": "TheEye", "536870920": "TheFish", "536870921": "TheFlint", "10": "TheGoad", "11": "TheHead", "12": "TheHook", "536870925": "TheHouse", "14": "TheManacle", "536870927": "TheMark", "536870928": "TheMouth", "536870929": "TheNeedle", "1610612754": "TheOx", "19": "ThePillar", "1073741844": "ThePlant", "21": "ThePsychic", "1342177302": "TheSerpent", "805306391": "TheTooth", "536870936": "TheWall", "536870937": "TheWater", "536870938": "TheWheel", "27": "TheWindow", "": -2147483648, "": -2147483647, "": -2147483646, "": -2147483645, "": -2147483644, "TheArm": 536870917, "": 6, "TheEye": 805306375, "TheFish": 536870920, "TheFlint": 536870921, "TheGoad": 10, "TheHead": 11, "TheHook": 12, "TheHouse": 536870925, "TheManacle": 14, "TheMark": 536870927, "TheMouth": 536870928, "TheNeedle": 536870929, "TheOx": 1610612754, "ThePillar": 19, "ThePlant": 1073741844, "ThePsychic": 21, "TheSerpent": 1342177302, "TheTooth": 805306391, "TheWall": 536870936, "TheWater": 536870937, "TheWheel": 536870938, "TheWindow": 27 },
    MotelyVoucher: { "0": "Overstock", "1": "OverstockPlus", "2": "ClearanceSale", "3": "Liquidation", "4": "Hone", "5": "GlowUp", "6": "RerollSurplus", "7": "RerollGlut", "8": "CrystalBall", "9": "OmenGlobe", "10": "Telescope", "11": "Observatory", "12": "Grabber", "13": "NachoTong", "14": "Wasteful", "15": "Recyclomancy", "16": "TarotMerchant", "17": "TarotTycoon", "18": "PlanetMerchant", "19": "PlanetTycoon", "20": "SeedMoney", "21": "MoneyTree", "22": "Blank", "23": "Antimatter", "24": "MagicTrick", "25": "Illusion", "26": "Hieroglyph", "27": "Petroglyph", "28": "DirectorsCut", "29": "Retcon", "30": "PaintBrush", "31": "Palette", "Overstock": 0, "OverstockPlus": 1, "ClearanceSale": 2, "Liquidation": 3, "Hone": 4, "GlowUp": 5, "RerollSurplus": 6, "RerollGlut": 7, "CrystalBall": 8, "OmenGlobe": 9, "Telescope": 10, "Observatory": 11, "Grabber": 12, "NachoTong": 13, "Wasteful": 14, "Recyclomancy": 15, "TarotMerchant": 16, "TarotTycoon": 17, "PlanetMerchant": 18, "PlanetTycoon": 19, "SeedMoney": 20, "MoneyTree": 21, "Blank": 22, "Antimatter": 23, "MagicTrick": 24, "Illusion": 25, "Hieroglyph": 26, "Petroglyph": 27, "DirectorsCut": 28, "Retcon": 29, "PaintBrush": 30, "Palette": 31 },
    MotelyTag: { "0": "UncommonTag", "1": "RareTag", "2": "NegativeTag", "3": "FoilTag", "4": "HolographicTag", "5": "PolychromeTag", "6": "InvestmentTag", "7": "VoucherTag", "8": "BossTag", "9": "StandardTag", "10": "CharmTag", "11": "MeteorTag", "12": "BuffoonTag", "13": "HandyTag", "14": "GarbageTag", "15": "EtherealTag", "16": "CouponTag", "17": "DoubleTag", "18": "JuggleTag", "19": "D6Tag", "20": "TopupTag", "21": "SpeedTag", "22": "OrbitalTag", "23": "EconomyTag", "UncommonTag": 0, "RareTag": 1, "NegativeTag": 2, "FoilTag": 3, "HolographicTag": 4, "PolychromeTag": 5, "InvestmentTag": 6, "VoucherTag": 7, "BossTag": 8, "StandardTag": 9, "CharmTag": 10, "MeteorTag": 11, "BuffoonTag": 12, "HandyTag": 13, "GarbageTag": 14, "EtherealTag": 15, "CouponTag": 16, "DoubleTag": 17, "JuggleTag": 18, "D6Tag": 19, "TopupTag": 20, "SpeedTag": 21, "OrbitalTag": 22, "EconomyTag": 23 },
    MotelyItemType: { "16384": "Mercury", "16385": "Venus", "16386": "Earth", "16387": "Mars", "16388": "Jupiter", "16389": "Saturn", "16390": "Uranus", "16391": "Neptune", "16392": "Pluto", "16393": "PlanetX", "16394": "Ceres", "16395": "Eris", "8192": "Familiar", "8193": "Grim", "8194": "Incantation", "8195": "Talisman", "8196": "Aura", "8197": "Wraith", "8198": "Sigil", "8199": "Ouija", "8200": "Ectoplasm", "8201": "Immolate", "8202": "Ankh", "8203": "DejaVu", "8204": "Hex", "8205": "Trance", "8206": "Medium", "8207": "Cryptid", "8208": "TheSoul", "8209": "BlackHole", "12288": "TheFool", "12289": "TheMagician", "12290": "TheHighPriestess", "12291": "TheEmpress", "12292": "TheEmperor", "12293": "TheHierophant", "12294": "TheLovers", "12295": "TheChariot", "12296": "Justice", "12297": "TheHermit", "12298": "TheWheelOfFortune", "12299": "Strength", "12300": "TheHangedMan", "12301": "Death", "12302": "Temperance", "12303": "TheDevil", "12304": "TheTower", "12305": "TheStar", "12306": "TheMoon", "12307": "TheSun", "12308": "Judgement", "12309": "TheWorld", "4096": "C2", "4097": "C3", "4098": "C4", "4099": "C5", "4100": "C6", "4101": "C7", "4102": "C8", "4103": "C9", "4104": "C10", "4105": "CJ", "4106": "CQ", "4107": "CK", "4108": "CA", "4112": "D2", "4113": "D3", "4114": "D4", "4115": "D5", "4116": "D6", "4117": "D7", "4118": "D8", "4119": "D9", "4120": "D10", "4121": "DJ", "4122": "DQ", "4123": "DK", "4124": "DA", "4128": "H2", "4129": "H3", "4130": "H4", "4131": "H5", "4132": "H6", "4133": "H7", "4134": "H8", "4135": "H9", "4136": "H10", "4137": "HJ", "4138": "HQ", "4139": "HK", "4140": "HA", "4144": "S2", "4145": "S3", "4146": "S4", "4147": "S5", "4148": "S6", "4149": "S7", "4150": "S8", "4151": "S9", "4152": "S10", "4153": "SJ", "4154": "SQ", "4155": "SK", "4156": "SA", "20480": "Joker", "20481": "GreedyJoker", "20482": "LustyJoker", "20483": "WrathfulJoker", "20484": "GluttonousJoker", "20485": "JollyJoker", "20486": "ZanyJoker", "20487": "MadJoker", "20488": "CrazyJoker", "20489": "DrollJoker", "20490": "SlyJoker", "20491": "WilyJoker", "20492": "CleverJoker", "20493": "DeviousJoker", "20494": "CraftyJoker", "20495": "HalfJoker", "20496": "CreditCard", "20497": "Banner", "20498": "MysticSummit", "20499": "EightBall", "20500": "Misprint", "20501": "RaisedFist", "20502": "ChaostheClown", "20503": "ScaryFace", "20504": "AbstractJoker", "20505": "DelayedGratification", "20506": "GrosMichel", "20507": "EvenSteven", "20508": "OddTodd", "20509": "Scholar", "20510": "BusinessCard", "20511": "Supernova", "20512": "RideTheBus", "20513": "Egg", "20514": "Runner", "20515": "IceCream", "20516": "Splash", "20517": "BlueJoker", "20518": "FacelessJoker", "20519": "GreenJoker", "20520": "Superposition", "20521": "ToDoList", "20522": "Cavendish", "20523": "RedCard", "20524": "SquareJoker", "20525": "RiffRaff", "20526": "Photograph", "20527": "ReservedParking", "20528": "MailInRebate", "20529": "Hallucination", "20530": "FortuneTeller", "20531": "Juggler", "20532": "Drunkard", "20533": "GoldenJoker", "20534": "Popcorn", "20535": "WalkieTalkie", "20536": "SmileyFace", "20537": "GoldenTicket", "20538": "Swashbuckler", "20539": "HangingChad", "20540": "ShootTheMoon", "21504": "JokerStencil", "21505": "FourFingers", "21506": "Mime", "21507": "CeremonialDagger", "21508": "MarbleJoker", "21509": "LoyaltyCard", "21510": "Dusk", "21511": "Fibonacci", "21512": "SteelJoker", "21513": "Hack", "21514": "Pareidolia", "21515": "SpaceJoker", "21516": "Burglar", "21517": "Blackboard", "21518": "SixthSense", "21519": "Constellation", "21520": "Hiker", "21521": "CardSharp", "21522": "Madness", "21523": "Seance", "21524": "Vampire", "21525": "Shortcut", "21526": "Hologram", "21527": "Cloud9", "21528": "Rocket", "21529": "MidasMask", "21530": "Luchador", "21531": "GiftCard", "21532": "TurtleBean", "21533": "Erosion", "21534": "ToTheMoon", "21535": "StoneJoker", "21536": "LuckyCat", "21537": "Bull", "21538": "DietCola", "21539": "TradingCard", "21540": "FlashCard", "21541": "SpareTrousers", "21542": "Ramen", "21543": "Seltzer", "21544": "Castle", "21545": "MrBones", "21546": "Acrobat", "21547": "SockAndBuskin", "21548": "Troubadour", "21549": "Certificate", "21550": "SmearedJoker", "21551": "Throwback", "21552": "RoughGem", "21553": "Bloodstone", "21554": "Arrowhead", "21555": "OnyxAgate", "21556": "GlassJoker", "21557": "Showman", "21558": "FlowerPot", "21559": "MerryAndy", "21560": "OopsAll6s", "21561": "TheIdol", "21562": "SeeingDouble", "21563": "Matador", "21564": "Satellite", "21565": "Cartomancer", "21566": "Astronomer", "21567": "Bootstraps", "22528": "DNA", "22529": "Vagabond", "22530": "Baron", "22531": "Obelisk", "22532": "BaseballCard", "22533": "AncientJoker", "22534": "Campfire", "22535": "Blueprint", "22536": "WeeJoker", "22537": "HitTheRoad", "22538": "TheDuo", "22539": "TheTrio", "22540": "TheFamily", "22541": "TheOrder", "22542": "TheTribe", "22543": "Stuntman", "22544": "InvisibleJoker", "22545": "Brainstorm", "22546": "DriversLicense", "22547": "BurntJoker", "23552": "Canio", "23553": "Triboulet", "23554": "Yorick", "23555": "Chicot", "23556": "Perkeo", "61440": "Invalid", "61441": "NotImplemented", "61442": "JokerExcludedByStream", "61443": "PlanetExcludedByStream", "61444": "TarotExcludedByStream", "61445": "SpectralExcludedByStream", "Mercury": 16384, "Venus": 16385, "Earth": 16386, "Mars": 16387, "Jupiter": 16388, "Saturn": 16389, "Uranus": 16390, "Neptune": 16391, "Pluto": 16392, "PlanetX": 16393, "Ceres": 16394, "Eris": 16395, "Familiar": 8192, "Grim": 8193, "Incantation": 8194, "Talisman": 8195, "Aura": 8196, "Wraith": 8197, "Sigil": 8198, "Ouija": 8199, "Ectoplasm": 8200, "Immolate": 8201, "Ankh": 8202, "DejaVu": 8203, "Hex": 8204, "Trance": 8205, "Medium": 8206, "Cryptid": 8207, "TheSoul": 8208, "BlackHole": 8209, "TheFool": 12288, "TheMagician": 12289, "TheHighPriestess": 12290, "TheEmpress": 12291, "TheEmperor": 12292, "TheHierophant": 12293, "TheLovers": 12294, "TheChariot": 12295, "Justice": 12296, "TheHermit": 12297, "TheWheelOfFortune": 12298, "Strength": 12299, "TheHangedMan": 12300, "Death": 12301, "Temperance": 12302, "TheDevil": 12303, "TheTower": 12304, "TheStar": 12305, "TheMoon": 12306, "TheSun": 12307, "Judgement": 12308, "TheWorld": 12309, "C2": 4096, "C3": 4097, "C4": 4098, "C5": 4099, "C6": 4100, "C7": 4101, "C8": 4102, "C9": 4103, "C10": 4104, "CJ": 4105, "CQ": 4106, "CK": 4107, "CA": 4108, "D2": 4112, "D3": 4113, "D4": 4114, "D5": 4115, "D6": 4116, "D7": 4117, "D8": 4118, "D9": 4119, "D10": 4120, "DJ": 4121, "DQ": 4122, "DK": 4123, "DA": 4124, "H2": 4128, "H3": 4129, "H4": 4130, "H5": 4131, "H6": 4132, "H7": 4133, "H8": 4134, "H9": 4135, "H10": 4136, "HJ": 4137, "HQ": 4138, "HK": 4139, "HA": 4140, "S2": 4144, "S3": 4145, "S4": 4146, "S5": 4147, "S6": 4148, "S7": 4149, "S8": 4150, "S9": 4151, "S10": 4152, "SJ": 4153, "SQ": 4154, "SK": 4155, "SA": 4156, "Joker": 20480, "GreedyJoker": 20481, "LustyJoker": 20482, "WrathfulJoker": 20483, "GluttonousJoker": 20484, "JollyJoker": 20485, "ZanyJoker": 20486, "MadJoker": 20487, "CrazyJoker": 20488, "DrollJoker": 20489, "SlyJoker": 20490, "WilyJoker": 20491, "CleverJoker": 20492, "DeviousJoker": 20493, "CraftyJoker": 20494, "HalfJoker": 20495, "CreditCard": 20496, "Banner": 20497, "MysticSummit": 20498, "EightBall": 20499, "Misprint": 20500, "RaisedFist": 20501, "ChaostheClown": 20502, "ScaryFace": 20503, "AbstractJoker": 20504, "DelayedGratification": 20505, "GrosMichel": 20506, "EvenSteven": 20507, "OddTodd": 20508, "Scholar": 20509, "BusinessCard": 20510, "Supernova": 20511, "RideTheBus": 20512, "Egg": 20513, "Runner": 20514, "IceCream": 20515, "Splash": 20516, "BlueJoker": 20517, "FacelessJoker": 20518, "GreenJoker": 20519, "Superposition": 20520, "ToDoList": 20521, "Cavendish": 20522, "RedCard": 20523, "SquareJoker": 20524, "RiffRaff": 20525, "Photograph": 20526, "ReservedParking": 20527, "MailInRebate": 20528, "Hallucination": 20529, "FortuneTeller": 20530, "Juggler": 20531, "Drunkard": 20532, "GoldenJoker": 20533, "Popcorn": 20534, "WalkieTalkie": 20535, "SmileyFace": 20536, "GoldenTicket": 20537, "Swashbuckler": 20538, "HangingChad": 20539, "ShootTheMoon": 20540, "JokerStencil": 21504, "FourFingers": 21505, "Mime": 21506, "CeremonialDagger": 21507, "MarbleJoker": 21508, "LoyaltyCard": 21509, "Dusk": 21510, "Fibonacci": 21511, "SteelJoker": 21512, "Hack": 21513, "Pareidolia": 21514, "SpaceJoker": 21515, "Burglar": 21516, "Blackboard": 21517, "SixthSense": 21518, "Constellation": 21519, "Hiker": 21520, "CardSharp": 21521, "Madness": 21522, "Seance": 21523, "Vampire": 21524, "Shortcut": 21525, "Hologram": 21526, "Cloud9": 21527, "Rocket": 21528, "MidasMask": 21529, "Luchador": 21530, "GiftCard": 21531, "TurtleBean": 21532, "Erosion": 21533, "ToTheMoon": 21534, "StoneJoker": 21535, "LuckyCat": 21536, "Bull": 21537, "DietCola": 21538, "TradingCard": 21539, "FlashCard": 21540, "SpareTrousers": 21541, "Ramen": 21542, "Seltzer": 21543, "Castle": 21544, "MrBones": 21545, "Acrobat": 21546, "SockAndBuskin": 21547, "Troubadour": 21548, "Certificate": 21549, "SmearedJoker": 21550, "Throwback": 21551, "RoughGem": 21552, "Bloodstone": 21553, "Arrowhead": 21554, "OnyxAgate": 21555, "GlassJoker": 21556, "Showman": 21557, "FlowerPot": 21558, "MerryAndy": 21559, "OopsAll6s": 21560, "TheIdol": 21561, "SeeingDouble": 21562, "Matador": 21563, "Satellite": 21564, "Cartomancer": 21565, "Astronomer": 21566, "Bootstraps": 21567, "DNA": 22528, "Vagabond": 22529, "Baron": 22530, "Obelisk": 22531, "BaseballCard": 22532, "AncientJoker": 22533, "Campfire": 22534, "Blueprint": 22535, "WeeJoker": 22536, "HitTheRoad": 22537, "TheDuo": 22538, "TheTrio": 22539, "TheFamily": 22540, "TheOrder": 22541, "TheTribe": 22542, "Stuntman": 22543, "InvisibleJoker": 22544, "Brainstorm": 22545, "DriversLicense": 22546, "BurntJoker": 22547, "Canio": 23552, "Triboulet": 23553, "Yorick": 23554, "Chicot": 23555, "Perkeo": 23556, "Invalid": 61440, "NotImplemented": 61441, "JokerExcludedByStream": 61442, "PlanetExcludedByStream": 61443, "TarotExcludedByStream": 61444, "SpectralExcludedByStream": 61445 },
    MotelyItemTypeCategory: { "4096": "PlayingCard", "8192": "SpectralCard", "12288": "TarotCard", "16384": "PlanetCard", "20480": "Joker", "61440": "Invalid", "PlayingCard": 4096, "SpectralCard": 8192, "TarotCard": 12288, "PlanetCard": 16384, "Joker": 20480, "Invalid": 61440 },
    MotelyItemSeal: { "0": "None", "65536": "Gold", "131072": "Red", "196608": "Blue", "262144": "Purple", "None": 0, "Gold": 65536, "Red": 131072, "Blue": 196608, "Purple": 262144 },
    MotelyItemEnhancement: { "0": "None", "524288": "Bonus", "1048576": "Mult", "1572864": "Wild", "2097152": "Glass", "2621440": "Steel", "3145728": "Stone", "3670016": "Gold", "4194304": "Lucky", "None": 0, "Bonus": 524288, "Mult": 1048576, "Wild": 1572864, "Glass": 2097152, "Steel": 2621440, "Stone": 3145728, "Gold": 3670016, "Lucky": 4194304 },
    MotelyItemEdition: { "0": "None", "8388608": "Foil", "16777216": "Holographic", "25165824": "Polychrome", "33554432": "Negative", "None": 0, "Foil": 8388608, "Holographic": 16777216, "Polychrome": 25165824, "Negative": 33554432 },
    MotelyPlayingCardSuit: { "0": "Clubs", "16": "Diamonds", "32": "Hearts", "48": "Spades", "Clubs": 0, "Diamonds": 16, "Hearts": 32, "Spades": 48 },
    MotelyPlayingCardRank: { "0": "Two", "1": "Three", "2": "Four", "3": "Five", "4": "Six", "5": "Seven", "6": "Eight", "7": "Nine", "8": "Ten", "9": "Jack", "10": "Queen", "11": "King", "12": "Ace", "Two": 0, "Three": 1, "Four": 2, "Five": 3, "Six": 4, "Seven": 5, "Eight": 6, "Nine": 7, "Ten": 8, "Jack": 9, "Queen": 10, "King": 11, "Ace": 12 },
    MotelyBoosterPack: { "0": "Arcana", "1": "JumboArcana", "2": "MegaArcana", "4": "Celestial", "5": "JumboCelestial", "6": "MegaCelestial", "8": "Standard", "9": "JumboStandard", "10": "MegaStandard", "12": "Buffoon", "13": "JumboBuffoon", "14": "MegaBuffoon", "16": "Spectral", "17": "JumboSpectral", "18": "MegaSpectral", "Arcana": 0, "JumboArcana": 1, "MegaArcana": 2, "Celestial": 4, "JumboCelestial": 5, "MegaCelestial": 6, "Standard": 8, "JumboStandard": 9, "MegaStandard": 10, "Buffoon": 12, "JumboBuffoon": 13, "MegaBuffoon": 14, "Spectral": 16, "JumboSpectral": 17, "MegaSpectral": 18 },
    BrowserWasm: {
        MotelyProgram: {
            getVersion: () => getExports().Bootsharp_Generated_Exports_Motely_BrowserWasm_JSMotelyProgram_GetVersion(),
            validateJaml: (jamlContent) => getExports().Bootsharp_Generated_Exports_Motely_BrowserWasm_JSMotelyProgram_ValidateJaml(jamlContent),
            analyzeSeed: (seed, deck, stake) => deserialize(getExports().Bootsharp_Generated_Exports_Motely_BrowserWasm_JSMotelyProgram_AnalyzeSeed(seed, serialize(deck), serialize(stake))),
            loadSeed: (seed, deck, stake) => deserialize(getExports().Bootsharp_Generated_Exports_Motely_BrowserWasm_JSMotelyProgram_LoadSeed(seed, serialize(deck), serialize(stake))),
            startSearch: (jamlContent, threadCount, batchCharCount, startBatch, endBatch) => getExports().Bootsharp_Generated_Exports_Motely_BrowserWasm_JSMotelyProgram_StartSearch(jamlContent, threadCount, batchCharCount, startBatch, endBatch),
            startSeedListSearch: (jamlContent, seedsCsv) => getExports().Bootsharp_Generated_Exports_Motely_BrowserWasm_JSMotelyProgram_StartSeedListSearch(jamlContent, seedsCsv),
            stopSearch: () => getExports().Bootsharp_Generated_Exports_Motely_BrowserWasm_JSMotelyProgram_StopSearch()
        },
        MotelyProgramCallbacks: {
            get onProgress() { return this.onProgressHandler; },
            set onProgress(handler) { this.onProgressHandler = handler; this.onProgressSerializedHandler = (seedsSearched, matchingSeeds, elapsedMs) => this.onProgressHandler(seedsSearched, matchingSeeds, elapsedMs); },
            get onProgressSerialized() { if (typeof this.onProgressHandler !== "function") throw Error("Failed to invoke 'Motely.BrowserWasm.MotelyProgramCallbacks.onProgress' from C#. Make sure to assign function in JavaScript."); return this.onProgressSerializedHandler; },
            get onResult() { return this.onResultHandler; },
            set onResult(handler) { this.onResultHandler = handler; this.onResultSerializedHandler = (seed, score) => this.onResultHandler(seed, score); },
            get onResultSerialized() { if (typeof this.onResultHandler !== "function") throw Error("Failed to invoke 'Motely.BrowserWasm.MotelyProgramCallbacks.onResult' from C#. Make sure to assign function in JavaScript."); return this.onResultSerializedHandler; },
            get onComplete() { return this.onCompleteHandler; },
            set onComplete(handler) { this.onCompleteHandler = handler; this.onCompleteSerializedHandler = (status, seedsSearched, matchingSeeds) => this.onCompleteHandler(status, seedsSearched, matchingSeeds); },
            get onCompleteSerialized() { if (typeof this.onCompleteHandler !== "function") throw Error("Failed to invoke 'Motely.BrowserWasm.MotelyProgramCallbacks.onComplete' from C#. Make sure to assign function in JavaScript."); return this.onCompleteSerializedHandler; }
        }
    }
};

var bindings = /*#__PURE__*/Object.freeze({
    __proto__: null,
    Motely: Motely
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
const mt$2 = true;

var dotnet_g = /*#__PURE__*/Object.freeze({
    __proto__: null,
    embedded: embedded$2,
    mt: mt$2
});

const embedded$1 = false;
const mt$1 = true;

var dotnet_native_g = /*#__PURE__*/Object.freeze({
    __proto__: null,
    embedded: embedded$1,
    mt: mt$1
});

const embedded = false;
const mt = true;

var dotnet_runtime_g = /*#__PURE__*/Object.freeze({
    __proto__: null,
    embedded: embedded,
    mt: mt
});

export { Event, Motely, index as default };
