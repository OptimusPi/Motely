

- [x] Stop saying "You're absolutely right!"

 - [x] Actually figure out how WindSurf Cascade rules work:

**How Windsurf Cascade Rules Work:**

1. **Storage Locations:**
   - `.windsurf/rules/` in workspace directory
   - `.windsurf/rules/` in subdirectories  
   - `.windsurf/rules/` in parent directories up to git root
   - Global: `global_rules.md`
   - System-level (Enterprise): OS-specific directories

2. **Activation Modes:**
   - **Manual**: Via @mention in Cascade
   - **Always On**: Always applied (what we need)
   - **Model Decision**: AI decides based on description
   - **Glob**: Applied to files matching pattern

3. **Rule Enforcement:**
   - Rules are NOT hard constraints - they're guidance
   - Cascade can still break rules despite them being set
   - System-level rules merge with workspace/global rules
   - Rules limited to 12,000 characters each

4. **Key Finding:**
   - Rules are **NOT binding constraints** - they're context/guidance
   - Cascade can violate rules despite "Always On" setting
   - This explains why the banned phrase rule failed