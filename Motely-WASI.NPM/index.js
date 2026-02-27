const { execFile } = require('child_process');
const { join } = require('path');
const { createInterface } = require('readline');

/**
 * motely-wasi — Node.js wrapper for the Motely WASI binary.
 * 
 * Uses stdin/stdout NDJSON protocol to communicate with the .NET WASI module.
 * Works with Node.js >= 16 (no native WASI module needed — spawns via wasmtime/wasmer).
 * 
 * Usage:
 *   const { MotelyWasi } = require('motely-wasi');
 *   const motely = await MotelyWasi.load();
 *   const result = await motely.validateJaml('name: Test\nmust:\n  - joker: Blueprint');
 *   const analysis = await motely.analyzeSeed('ABC123', 'Red', 'White');
 *   motely.close();
 */

class MotelyWasi {
    constructor(process) {
        this._process = process;
        this._pending = new Map();
        this._nextId = 1;

        // Read NDJSON responses from stdout
        this._rl = createInterface({ input: process.stdout });
        this._rl.on('line', (line) => {
            try {
                const parsed = JSON.parse(line);
                // Resolve the oldest pending call (FIFO)
                if (this._pending.size > 0) {
                    const [id, resolve] = this._pending.entries().next().value;
                    this._pending.delete(id);
                    resolve(parsed.result ?? parsed);
                }
            } catch { /* ignore malformed lines */ }
        });

        process.stderr?.on('data', (data) => {
            console.error('[motely-wasi]', data.toString());
        });
    }

    /**
     * Load the Motely WASI binary.
     * @param {Object} opts
     * @param {'wasmtime'|'wasmer'|'node'} opts.runtime - WASI runtime to use (default: 'wasmtime')
     * @param {string} opts.wasmPath - Path to the .wasm binary (default: bundled)
     */
    static async load(opts = {}) {
        const runtime = opts.runtime || 'wasmtime';
        const wasmPath = opts.wasmPath || join(__dirname, 'wasm', 'Motely.WASI.wasm');

        let args;
        switch (runtime) {
            case 'wasmtime':
                args = ['wasmtime', ['run', '--', wasmPath]];
                break;
            case 'wasmer':
                args = ['wasmer', ['run', wasmPath]];
                break;
            case 'node':
                // Use Node.js built-in WASI (experimental)
                args = ['node', ['--experimental-wasi-unstable-preview1', '-e', `
          const { readFileSync } = require('fs');
          const { WASI } = require('wasi');
          const wasi = new WASI({ args: process.argv, env: process.env });
          const wasm = readFileSync('${wasmPath.replace(/\\/g, '\\\\')}');
          WebAssembly.compile(wasm).then(mod =>
            WebAssembly.instantiate(mod, { wasi_snapshot_preview1: wasi.wasiImport })
          ).then(inst => wasi.start(inst));
        `]];
                break;
            default:
                throw new Error(`Unknown runtime: ${runtime}`);
        }

        const proc = execFile(args[0], args[1], {
            stdio: ['pipe', 'pipe', 'pipe'],
            maxBuffer: 50 * 1024 * 1024,
        });

        // Wait for the process to be ready (give it a moment to boot)
        await new Promise(r => setTimeout(r, 300));

        return new MotelyWasi(proc);
    }

    _call(method, params) {
        return new Promise((resolve, reject) => {
            const id = this._nextId++;
            const timeout = setTimeout(() => {
                this._pending.delete(id);
                reject(new Error(`motely-wasi: ${method} timed out after 30s`));
            }, 30000);

            this._pending.set(id, (result) => {
                clearTimeout(timeout);
                resolve(result);
            });

            const msg = JSON.stringify({ method, params }) + '\n';
            this._process.stdin.write(msg);
        });
    }

    async validateJaml(jaml) {
        return this._call('validate_jaml', { jaml });
    }

    async analyzeSeed(seed, deck, stake) {
        return this._call('analyze_seed', { seed, deck, stake });
    }

    async getCapabilities() {
        return this._call('get_capabilities', {});
    }

    close() {
        this._rl?.close();
        this._process?.stdin?.end();
        this._process?.kill();
    }
}

module.exports = { MotelyWasi };
