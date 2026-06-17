#!/usr/bin/env node
/**
 * jaml-seed CLI
 *
 * Commands:
 *   jaml-seed login       → npm login (interactive auth)
 *   jaml-seed whoami      → show current npm user
 *   jaml-seed publish     → typecheck + build lib + npm publish --access public
 *   jaml-seed publish:beta  → publish with beta tag
 *   jaml-seed dev         → next dev (run the app locally)
 *   jaml-seed build       → next build (production build)
 *   jaml-seed build:lib   → vite build (library build)
 *   jaml-seed lint        → eslint + tsc
 *   jaml-seed init        → scaffold a new jaml-seed-lab project
 *   jaml-seed help        → show this message
 *
 * Environment:
 *   NPM_REGISTRY      — custom registry (default: https://registry.npmjs.org)
 *   NPM_ACCESS_TOKEN  — CI token for automated publishing
 */

import { execSync, spawn } from "node:child_process";
import { createRequire } from "node:module";
import { readFileSync, writeFileSync, mkdirSync, existsSync } from "node:fs";
import { join, dirname } from "node:path";
import { fileURLToPath } from "node:url";

const __dirname = dirname(fileURLToPath(import.meta.url));
const ROOT = join(__dirname, "..");

const pkg = JSON.parse(readFileSync(join(ROOT, "package.json"), "utf-8"));

const COMMANDS = {
  login() {
    console.log("🔐 Authenticating with npm…");
    execSync("npm login", { stdio: "inherit", cwd: ROOT });
    console.log("✅ Logged in. Run `jaml-seed whoami` to verify.");
  },

  whoami() {
    try {
      const user = execSync("npm whoami", { encoding: "utf-8", cwd: ROOT }).trim();
      console.log(`👤 Logged in as: ${user}`);
    } catch {
      console.log("❌ Not logged in. Run `jaml-seed login` first.");
      process.exit(1);
    }
  },

  publish() {
    this.whoami();
    console.log(`📦 Publishing ${pkg.name}@${pkg.version}…`);
    execSync("npm run prepublishOnly", { stdio: "inherit", cwd: ROOT });
    execSync("npm publish --access public", { stdio: "inherit", cwd: ROOT });
    console.log(`✅ Published ${pkg.name}@${pkg.version}`);
  },

  "publish:beta"() {
    this.whoami();
    console.log(`📦 Publishing ${pkg.name}@${pkg.version} (beta tag)…`);
    execSync("npm run prepublishOnly", { stdio: "inherit", cwd: ROOT });
    execSync("npm publish --access public --tag beta", { stdio: "inherit", cwd: ROOT });
    console.log(`✅ Published ${pkg.name}@${pkg.version} as beta`);
  },

  dev() {
    console.log("🚀 Starting dev server…");
    const proc = spawn("npx", ["next", "dev"], { stdio: "inherit", cwd: ROOT, shell: true });
    proc.on("exit", (code) => process.exit(code ?? 0));
  },

  build() {
    console.log("🔨 Building Next.js app for production…");
    execSync("npx next build", { stdio: "inherit", cwd: ROOT });
    console.log("✅ Build complete.");
  },

  "build:lib"() {
    console.log("🔨 Building library (vite)…");
    execSync("npx vite build --config vite.lib.config.ts", { stdio: "inherit", cwd: ROOT });
    console.log("✅ Library build complete.");
  },

  lint() {
    console.log("🔍 Linting…");
    try {
      execSync("npx eslint .", { stdio: "inherit", cwd: ROOT });
    } catch {
      // eslint exits 1 on errors, that's expected
    }
    console.log("🔍 Type checking…");
    try {
      execSync("npx tsc --noEmit", { stdio: "inherit", cwd: ROOT });
    } catch {
      // tsc exits 1 on errors
    }
  },

  init() {
    const target = process.argv[3] || ".";
    const absTarget = join(process.cwd(), target);
    if (!existsSync(absTarget)) mkdirSync(absTarget, { recursive: true });

    console.log(`🌱 Scaffolding jaml-seed-lab project in ${target}…`);

    const scaffold = {
      "package.json": JSON.stringify({
        name: "my-balatro-seed-app",
        version: "0.1.0",
        private: true,
        type: "module",
        scripts: {
          dev: "next dev",
          build: "next build",
          start: "next start"
        },
        dependencies: {
          "jaml-seed-lab": "^0.1.0",
          next: "^16.2.9",
          react: "^19.2.7",
          "react-dom": "^19.2.7"
        }
      }, null, 2),
      "app/layout.tsx": `import { JamlSeedLabLayout } from "jaml-seed-lab/apps/home";
export default function RootLayout({ children }: { children: React.ReactNode }) {
  return <JamlSeedLabLayout>{children}</JamlSeedLabLayout>;
}`,
      "app/page.tsx": `export { JamlSeedLabHomePage as default } from "jaml-seed-lab/apps/home";`,
      "app/ide/page.tsx": `export { JamlIdePage as default } from "jaml-seed-lab/apps/ide";`,
      "app/finder/page.tsx": `export { SeedFinderPage as default } from "jaml-seed-lab/apps/finder";`,
      "app/analyzer/page.tsx": `export { JamlSeedAnalyzerPage as default } from "jaml-seed-lab/apps/analyzer";`,
      "tsconfig.json": JSON.stringify({
        compilerOptions: {
          lib: ["dom", "dom.iterable", "esnext"],
          allowJs: true, skipLibCheck: true, strict: true, noEmit: true,
          esModuleInterop: true, module: "esnext", moduleResolution: "bundler",
          resolveJsonModule: true, isolatedModules: true, jsx: "preserve",
          incremental: true, plugins: [{ name: "next" }],
          paths: { "@/*": ["./*"] }
        },
        include: ["next-env.d.ts", "**/*.ts", "**/*.tsx", ".next/types/**/*.ts"],
        exclude: ["node_modules"]
      }, null, 2)
    };

    for (const [file, content] of Object.entries(scaffold)) {
      const path = join(absTarget, file);
      mkdirSync(dirname(path), { recursive: true });
      writeFileSync(path, content);
      console.log(`  ✓ ${file}`);
    }

    console.log(`\n✅ Scaffolded! Run:\n  cd ${target} && npm install && npm run dev`);
  },

  help() {
    console.log(`
jaml-seed CLI — Balatro Seed Lab v${pkg.version}

Commands:
  login              Authenticate with npm (interactive)
  whoami             Show current npm user
  publish            Build + publish to npm (public access)
  publish:beta       Build + publish with beta tag
  dev                Run Next.js dev server (http://localhost:3000)
  build              Build Next.js app for production
  build:lib          Build library bundle with Vite
  lint               Run ESLint + TypeScript type check
  init [dir]         Scaffold a new jaml-seed-lab project
  help               Show this message

Examples:
  jaml-seed login
  jaml-seed publish
  jaml-seed init my-seed-app
  jaml-seed dev

Package: ${pkg.name}
Registry: ${process.env.NPM_REGISTRY || "https://registry.npmjs.org"}
`);
  }
};

const cmd = process.argv[2] || "help";
const handler = COMMANDS[cmd];
if (!handler) {
  console.error(`Unknown command: ${cmd}. Run \`jaml-seed help\` for usage.`);
  process.exit(1);
}
handler.call(COMMANDS);
