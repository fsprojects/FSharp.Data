# Repo Assist Memory

## Last Updated
2026-03-03

## Recent Actions
- 2026-03-03: Rebased PR #1665 onto main (resolved RELEASE_NOTES.md conflict: combined encoding change + ExceptionIfMissing under 9.0.0). Updated monthly summary #1684.
- 2026-03-02: Push to PR #1665 failed (patch apply error — merge conflict with c87020d3 split commit). Issue #1687 auto-created.
- 2026-03-01: #1681, #1685, #1683, #1686 all merged to main by dsyme. Issues #1677, #1678, #1679 closed.

## Version Status
- Current main RELEASE_NOTES.md version: `8.1.0-beta`
- PR #1665 branch version: `9.0.0` (per dsyme's request for major bump due to breaking encoding change)

## Open Repo Assist PRs
- #1676: schema.org microdata + JSON-LD for HtmlProvider — awaiting review from dsyme
- #1665: HTTP encoding ISO-8859-1 → UTF-8 — version bumped to 9.0.0, rebased 2026-03-03

## Monthly Activity Issue
- Issue #1684: [Repo Assist] Monthly Activity 2026-03 — updated 2026-03-03

## Notes
- DesignTime signature tests (FSharp.Data.DesignTime.Tests) are pre-existing infrastructure failures
- The full build (RunTests) sometimes fails with OOM (exit code 137) on this runner
- dsyme has been very actively merging PRs - 25+ merged in Feb-Mar 2026
- Issues #1677 (split large files), #1678 (document codebase), #1679 (transient failure) are CLOSED
- PR #1665 version is 9.0.0 - the encoding change is a breaking change per dsyme
- Issue #1687 (workflow failure) was caused by failed patch push for PR #1665; resolved in 2026-03-03 run
