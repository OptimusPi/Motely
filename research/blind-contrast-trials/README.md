# Blind Contrast Trials

Use this launcher to compare two operator-entered prompt variants while retaining no variant,
prompt, or model-output text on disk.

```powershell
./Run-BlindContrastTrial.ps1 -Trials 12 -OutputPath results.csv
```

Enter the shared task and the two variants only at runtime. For each randomized trial, paste the
displayed prompt into the selected model and score the observed response. The CSV records only:

- study ID and model/version
- randomized condition code (`A` or `B`)
- task completion
- unsolicited caretaker/crisis framing
- unwanted identity/state inference
- task override
- an optional redacted note

Do not paste prompts, variants, raw output, account information, or confidential material into the
notes field. Keep raw evidence separately only where you are authorized to store it.

For a publishable comparison, run the same number of trials per condition, randomize order, define
the scoring rubric before collecting data, and have a reviewer score redacted outputs without
seeing the condition code.