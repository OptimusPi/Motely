#!/usr/bin/env node
import { execFileSync } from "node:child_process";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";

const __dirname = dirname(fileURLToPath(import.meta.url));
const repoRoot = resolve(__dirname, "../../..");
const cliProject = resolve(repoRoot, "Motely.CLI");

execFileSync(
  "dotnet",
  ["run", "--project", cliProject, "--", "--write-jaml-schema"],
  {
    cwd: repoRoot,
    stdio: "inherit",
  },
);
