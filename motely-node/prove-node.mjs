#!/usr/bin/env node
/**
 * Proof that motely-node works. Run: node prove-node.mjs
 * Writes result to prove-node-result.txt.
 */
import { writeFileSync } from 'fs';

const lines = [];
function out(msg) {
  lines.push(msg);
}

out('prove-node: start');
try {
  const { loadMotely } = await import('./index.js');
  out('prove-node: loadMotely imported');
  const motely = await loadMotely();
  out('prove-node: loadMotely() done');
  const caps = await motely.getCapabilities();
  out('prove-node: getCapabilities() = ' + JSON.stringify(caps, null, 2));
  const jaml = 'deck: Red\nstake: White\nmust:\n  - joker: Joker';
  const valid = await motely.validateJaml(jaml);
  out('prove-node: validateJaml() = ' + JSON.stringify(valid));
  const analysis = await motely.analyzeSeed('ABCD1234', 'Red', 'White');
  out('prove-node: analyzeSeed() = seed=' + analysis.seed + ' antes=' + (analysis.antes?.length ?? 0));
  out('prove-node: OK');
} catch (e) {
  out('prove-node: FAIL ' + (e?.message || e));
  out((e?.stack || '').toString());
  process.exit(1);
} finally {
  writeFileSync('prove-node-result.txt', lines.join('\n'));
}
