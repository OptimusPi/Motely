// Prepends a guarded Uint8Array base64/hex polyfill to dist/index.mjs, the
// package's guaranteed first-loaded entry (package.json main/import). Runs
// as a postbuild step after `dotnet publish` regenerates dist/ from scratch.
//
// Why: config.mjs's resolveBinary() calls Uint8Array.fromBase64 to decode the
// embedded .NET assemblies during bootsharp.boot(). That TC39 method is native
// only on newer runtimes (Node 24+ / recent V8) — older server Node (e.g. some
// Vercel serverless runtimes) throws, so every server-side boot fails before
// any Motely API can run. Browsers already ship these methods natively, so
// each install below is guarded — the real (spec) method wins if present, and
// on any environment that already has it (every browser, newer Node) this is
// a complete no-op that never touches `Buffer`.
import { readFileSync, writeFileSync } from "node:fs";
import { fileURLToPath } from "node:url";
import { dirname, join } from "node:path";

const here = dirname(fileURLToPath(import.meta.url));
const indexPath = join(here, "..", "dist", "index.mjs");

const MARKER = "// __uint8-base64-polyfill__";

const polyfill = `${MARKER}
const __U8 = Uint8Array;
const __proto = Uint8Array.prototype;
if (typeof __U8.fromBase64 !== "function") {
    __U8.fromBase64 = (s, opts) => {
        const enc = opts?.alphabet === "base64url" ? "base64url" : "base64";
        return new Uint8Array(Buffer.from(s, enc));
    };
}
if (typeof __U8.fromHex !== "function") {
    __U8.fromHex = (s) => new Uint8Array(Buffer.from(s, "hex"));
}
if (typeof __proto.toBase64 !== "function") {
    __proto.toBase64 = function (opts) {
        const enc = opts?.alphabet === "base64url" ? "base64url" : "base64";
        return Buffer.from(this.buffer, this.byteOffset, this.byteLength).toString(enc);
    };
}
if (typeof __proto.toHex !== "function") {
    __proto.toHex = function () {
        return Buffer.from(this.buffer, this.byteOffset, this.byteLength).toString("hex");
    };
}
if (typeof __proto.setFromBase64 !== "function") {
    __proto.setFromBase64 = function (s) {
        const bytes = Buffer.from(s, "base64");
        const written = Math.min(bytes.length, this.byteLength);
        this.set(bytes.subarray(0, written));
        return { read: written, written };
    };
}
if (typeof __proto.setFromHex !== "function") {
    __proto.setFromHex = function (s) {
        const bytes = Buffer.from(s, "hex");
        const written = Math.min(bytes.length, this.byteLength);
        this.set(bytes.subarray(0, written));
        return { read: written, written };
    };
}
`;

const original = readFileSync(indexPath, "utf8");
if (original.includes(MARKER)) {
    console.log("patch-dist-base64-polyfill: already applied, skipping");
} else {
    writeFileSync(indexPath, polyfill + original, "utf8");
    console.log("patch-dist-base64-polyfill: prepended to dist/index.mjs");
}
