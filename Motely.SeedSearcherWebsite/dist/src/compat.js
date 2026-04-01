import dotnet, { Motely } from "motely-wasm";
import { bootAndWire } from "./shared.js";

await bootAndWire(dotnet, Motely, "compat");
