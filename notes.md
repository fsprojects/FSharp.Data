# Repo Assist Memory

## Last Updated
2026-03-05

## Recent Actions
- 2026-03-05: Updated PR #1665 — pushed RELEASE_NOTES.md fix: added ExceptionIfMissing + trimming entries to 9.0.0 section. Commented on #1687 confirming resolved. Updated monthly summary #1684.
- 2026-03-04: Push to PR #1665 failed again (patch apply error — shallow clone issue).
- 2026-03-03: Push to PR #1665 failed (patch apply error). 
- 2026-03-01: Created PRs #1681, #1683, #1685. Issues #1681, #1683, #1685, #1686 all merged to main by dsyme.

## Version Status
- Current main RELEASE_NOTES.md version: `8.1.0-beta` (unreleased)
- PR #1665 branch version: `9.0.0` (encoding change is a breaking change per dsyme)

## Open Repo Assist PRs
- #1676: schema.org microdata for HtmlProvider — mergeable_state: clean, awaiting review
- #1665: HTTP encoding ISO-8859-1 → UTF-8 — updated 2026-03-05, should now be clean

## Monthly Activity Issue
- Issue #1684: [Repo Assist] Monthly Activity 2026-03 — updated 2026-03-05

## Notes
- DesignTime signature tests (FSharp.Data.DesignTime.Tests) are pre-existing infrastructure failures
- The full build (RunTests) sometimes fails with OOM (exit code 137) on this runner
- dsyme has been very actively merging PRs - 25+ merged in Feb-Mar 2026
- PR #1665 push technique: use local branch named same as remote (tracking origin/...), 1 commit ahead
- Issue #1687 (workflow failure) was resolved in 2026-03-05 run; PR #1665 branch updated successfully
