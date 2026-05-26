import { execSync } from "node:child_process";
import { readFile } from "node:fs/promises";
import { resolve, dirname } from "node:path";
import { fileURLToPath } from "node:url";

const bump = (process.argv[2] ?? "patch") as "patch" | "minor" | "major";
if (!["patch", "minor", "major"].includes(bump)) {
  throw new Error(`Invalid bump: ${bump}. Use patch|minor|major.`);
}

const dir = resolve(dirname(fileURLToPath(import.meta.url)), "..");

function run(cmd: string, capture = false): string {
  console.log(`\n$ ${cmd}`);
  if (capture) return execSync(cmd, { cwd: dir }).toString().trim();
  execSync(cmd, { cwd: dir, stdio: "inherit" });
  return "";
}

const status = run("git status --short", true);
if (status) throw new Error(`Uncommitted changes:\n${status}`);

run("git fetch origin");
run("git pull --ff-only origin master");
run("pnpm install");
run("pnpm run build");
run(`pnpm version ${bump} --no-git-tag-version`);

const pkg = JSON.parse(await readFile(resolve(dir, "package.json"), "utf8"));
const version: string = pkg.version;
console.log(`\n→ jaml-ui bumped to ${version}`);

run("git add package.json");
run(`git commit -m "v${version}"`);
run(`git tag v${version}`);
run("git push origin master");
run(`git push origin v${version}`);
run("pnpm publish --no-git-checks");

console.log(`\n✓ jaml-ui ${version} published to npm.`);
