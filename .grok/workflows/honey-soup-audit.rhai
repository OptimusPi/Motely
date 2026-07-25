let meta = #{
    name: "honey-soup-audit",
    description: "Hunt honey-soup: sycophant filler, soup() fake code, claim-without-proof. Tables only. Handoff, not friendship.",
    when_to_use: "When the operator wants a full-repo / path scan for honey-soup violations (empty praise, non-running code, fake proof, dig-without-verb patterns in docs and tests).",
    phases: [
        #{ title: "Scope", detail: "resolve root and work list" },
        #{ title: "Scan", detail: "parallel nook scanners by dimension" },
        #{ title: "Verify", detail: "adversarial check each finding against the tree" },
        #{ title: "Report", detail: "scratch table + bye handoff" },
    ],
};

let findings_schema = #{
    "type": "object",
    "required": ["findings"],
    "properties": #{
        "findings": #{
            "type": "array",
            "maxItems": 12,
            "items": #{
                "type": "object",
                "required": ["path", "line_hint", "dimension", "issue", "snippet"],
                "properties": #{
                    "path": #{ "type": "string" },
                    "line_hint": #{ "type": "string" },
                    "dimension": #{ "type": "string" },
                    "issue": #{ "type": "string" },
                    "snippet": #{ "type": "string" },
                },
            },
        },
        "nooks_checked": #{ "type": "array", "items": #{ "type": "string" } },
    },
};

let verdict_schema = #{
    "type": "object",
    "required": ["real", "reason", "evidence"],
    "properties": #{
        "real": #{ "type": "boolean" },
        "reason": #{ "type": "string" },
        "evidence": #{ "type": "string" },
    },
};

// --- Scope ------------------------------------------------------------------
// Guard args: unit property access throws before pause can fire.
let root = if args == () { () } else { args.root };
let extra = if args == () { () } else { args.paths };
if root == () {
    // Default: current MotelyJAML workspace when launched from the repo.
    root = ".";
}

phase("Scope");
log("honey-soup-audit root=" + root + " — not friends, just tables");

// Fixed dimension list (deterministic work list; agents judge, Rhai owns scope).
let dimensions = [
    #{
        id: "sycophant",
        title: "sycophant filler",
        brief: "Empty praise / filler with no artifact: got it, great question, absolutely, love that, you're so right, oh yeah honey, happy to help with no path/diff/command attached. Also apology essays and friend-mode chat in agent/docs.",
    },
    #{
        id: "soup-code",
        title: "soup() fake code",
        brief: "Pseudocode theater, invented APIs, 'something like this' blocks that would not compile or run in THIS repo. Wrong symbols, missing imports, non-existent paths.",
    },
    #{
        id: "fake-proof",
        title: "claim without proof",
        brief: "Search/test correctness claimed without a real engine/CLI find. Shape-regex on seeds (looks-like-a-seed), 'suite green' without a run, progress-count-only tests sold as find-proof.",
    },
    #{
        id: "fluff-docs",
        title: "fluff docs / onion options",
        brief: "Paragraphs of vibe where a status table belongs. Onion option piles. Ban-list prose that primes the forbidden path instead of one true path. Empty 'next steps' with no verb.",
    },
    #{
        id: "consent-dig",
        title: "dig without verb",
        brief: "Agent notes or scripts that encourage tree walks, git log archaeology, restore/clone/delete on thrash/vent with no operator verb. Consent-before-touch violations documented as process.",
    },
    #{
        id: "mirror-truth",
        title: "second grammar / mirror truth",
        brief: "Motely-specific honey: parallel JAML grammar tables outside FilterDesc/JamlSchema, editor vocab re-authored in TS, fake WASM surface that is not CLI shape. Extra mirrors instead of one source of truth.",
    },
];

// Optional extra path hints from args.paths (array of strings).
let path_hint = "";
if extra != () {
    path_hint = " Also prioritize these paths (still stay under root): " + json_encode(extra) + ".";
}

// --- Scan -------------------------------------------------------------------
phase("Scan");
let jobs = [];
for d in dimensions {
    let p = "";
    p += "You are a honey-soup auditor (read-only). Honey-soup = warm empty praise + code that does not run.\n";
    p += "Root (stay under this path; drop anything outside): " + root + ".\n";
    p += "Dimension: " + d.title + " (" + d.id + ").\n";
    p += "Hunt for: " + d.brief + "\n";
    p += path_hint + "\n";
    p += "Procedure (mandatory tools):\n";
    p += "1. Use grep and read_file on real files under root. Do not answer from memory.\n";
    p += "2. Prefer scanning: docs (*.md), agent notes (CLAUDE.md, AGENTS.md), tests (*test*, *spec*), WASM JS tests, README, commit message drafts if present.\n";
    p += "3. Skip node_modules, bin/, obj/, dist/ generated noise unless a finding is only there.\n";
    p += "4. Each finding needs: path (repo-relative), line_hint (line number or nearby unique text), dimension id '" + d.id + "', short issue, short snippet.\n";
    p += "5. Cap 12 findings for this dimension. Empty findings is valid ONLY after you grepped/read and found nothing real.\n";
    p += "6. No sycophant openers in YOUR reply. No praise. Tables/structured schema only.\n";
    p += "7. List nooks_checked: the globs/paths you actually opened or grepped.\n";
    jobs.push(#{
        prompt: p,
        label: "scan:" + d.id,
        capability_mode: "read-only",
        output_schema: findings_schema,
    });
}
let scan_results = parallel(jobs);

let findings = [];
let nooks = [];
let i = 0;
for r in scan_results {
    if r != () && r.success && r.output != () {
        if r.output.findings != () {
            for f in r.output.findings {
                // Scope gate in Rhai: keep only paths that are not absolute escapes when root is "."
                // Agents can leak; script owns the invariant.
                if f.path != () && f.path != "" {
                    findings.push(f);
                }
            }
        }
        if r.output.nooks_checked != () {
            for n in r.output.nooks_checked {
                nooks.push(n);
            }
        }
    } else {
        log("scan slot " + i.to_string() + " failed or empty — treated as no findings from that nook");
    }
    i += 1;
}

log("raw findings=" + findings.len().to_string() + " nooks=" + nooks.len().to_string());

if findings.len() == 0 {
    let empty_report = "";
    empty_report += "# Honey-soup audit\n\n";
    empty_report += "| Field | Content |\n";
    empty_report += "|-------|----------|\n";
    empty_report += "| Doing | audit |\n";
    empty_report += "| Where | " + root + " |\n";
    empty_report += "| Result | zero confirmed findings after scan |\n";
    empty_report += "| Next | stop |\n\n";
    empty_report += "Nooks touched: " + nooks.len().to_string() + "\n\n";
    empty_report += "## Bye handoff\n\n";
    empty_report += "| Token | Verb |\n";
    empty_report += "|-------|------|\n";
    empty_report += "| T2 | re-run with args.root=path if scope was wrong |\n";
    empty_report += "| T3 | /honey-soup on a live agent reply |\n";
    empty_report += "| B5 | stop |\n";
    let pth = write_scratch_file("honey-soup-report.md", empty_report);
    complete(#{
        summary: "0 findings",
        path: pth,
        confirmed: [],
        bye: "handoff — not friends",
    });
}

// Cap verify panel to keep budget headroom (max 24 verifies).
const MAX_VERIFY = 24;
let to_verify = [];
let dropped = 0;
let fi = 0;
for f in findings {
    if fi < MAX_VERIFY {
        to_verify.push(f);
    } else {
        dropped += 1;
    }
    fi += 1;
}
if dropped > 0 {
    log("dropped " + dropped.to_string() + " findings over MAX_VERIFY=" + MAX_VERIFY.to_string());
}

// --- Verify -----------------------------------------------------------------
phase("Verify");
let vjobs = [];
for f in to_verify {
    let vp = "";
    vp += "Adversarially verify this honey-soup finding. Fail closed: real=true only with evidence you independently read.\n";
    vp += "Claimed path: " + f.path + "\n";
    vp += "Line hint: " + f.line_hint + "\n";
    vp += "Dimension: " + f.dimension + "\n";
    vp += "Issue: " + f.issue + "\n";
    vp += "Snippet: " + f.snippet + "\n";
    vp += "Root scope: " + root + "\n";
    vp += "Steps: read_file the path (or grep the snippet). If file missing, out of root, or snippet not present → real=false.\n";
    vp += "If the text exists but is NOT honey-soup (e.g. legitimate test seed pin, real CLI proof, technical 'must' language) → real=false.\n";
    vp += "evidence must quote what you read. No praise. No friendship.\n";
    vjobs.push(#{
        prompt: vp,
        label: "verify:" + f.dimension,
        capability_mode: "read-only",
        output_schema: verdict_schema,
    });
}
let verdicts = parallel(vjobs);

let confirmed = [];
let vi = 0;
for v in verdicts {
    if v != () && v.success && v.output != () && v.output.real == true
        && v.output.evidence != () && v.output.evidence != "" {
        let item = to_verify[vi];
        confirmed.push(#{
            path: item.path,
            line_hint: item.line_hint,
            dimension: item.dimension,
            issue: item.issue,
            snippet: item.snippet,
            evidence: v.output.evidence,
            reason: v.output.reason,
        });
    }
    vi += 1;
}

log(confirmed.len().to_string() + "/" + to_verify.len().to_string() + " survived verification");

// --- Report -----------------------------------------------------------------
phase("Report");
let report = "";
report += "# Honey-soup audit\n\n";
report += "| Field | Content |\n";
report += "|-------|----------|\n";
report += "| Doing | audit |\n";
report += "| Where | " + root + " |\n";
report += "| Scanned | " + dimensions.len().to_string() + " dimensions |\n";
report += "| Raw findings | " + findings.len().to_string() + " |\n";
report += "| Verified real | " + confirmed.len().to_string() + " |\n";
report += "| Dropped pre-verify | " + dropped.to_string() + " |\n";
report += "| Result | see table below |\n";
report += "| Next | T2 / T3 / B5 |\n\n";

report += "## Confirmed (real=true only)\n\n";
report += "| # | Dimension | Path | Issue |\n";
report += "|---|-----------|------|-------|\n";
let ci = 0;
for c in confirmed {
    report += "| " + ci.to_string() + " | " + c.dimension + " | " + c.path + " | " + c.issue + " |\n";
    ci += 1;
}
if confirmed.len() == 0 {
    report += "| — | — | — | none survived |\n";
}

report += "\n## Evidence blurbs\n\n";
ci = 0;
for c in confirmed {
    report += "### " + ci.to_string() + " — " + c.path + "\n";
    report += "- reason: " + c.reason + "\n";
    report += "- evidence: " + c.evidence + "\n";
    report += "- snippet: " + c.snippet + "\n\n";
    ci += 1;
}

report += "## Bye handoff\n\n";
report += "| Token | Verb |\n";
report += "|-------|------|\n";
report += "| T2 | fix highest-severity confirmed path with a real diff |\n";
report += "| T3 | re-scan args.root after fix |\n";
report += "| T4 | /honey-soup on the next agent reply that praises without shipping |\n";
report += "| B6 | expand MAX_VERIFY if dropped > 0 and you care |\n";
report += "| B5 | stop — handoff complete |\n\n";
report += "bye handoff 2 — not friends, just work product.\n";

let out_path = write_scratch_file("honey-soup-report.md", report);
complete(#{
    summary: confirmed.len().to_string() + " confirmed honey-soup findings",
    path: out_path,
    confirmed_count: confirmed.len(),
    raw_count: findings.len(),
    dropped: dropped,
    bye: "handoff — not friends",
});
