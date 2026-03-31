import dotnet, { Motely } from "motely-wasm-mt";
import { bootAndWire } from "./shared.js";

await bootAndWire(dotnet, Motely, "coep-sab");
