# Repo Assist Memory

## Last Updated
2026-03-04

## Recent Actions
- 2026-03-04: Rebased PR #1665 onto main (resolved RELEASE_NOTES.md conflict: combined encoding change + ExceptionIfMissing both under 9.0.0). Updated monthly summary #1684. Cleaned up stale items.
- 2026-03-03: Rebased PR #1665 onto main (resolved RELEASE_NOTES.md conflict). Updated monthly summary #1684.
- 2026-03-01: PRs #1681, #1683, #1685, #1686 all merged to main by dsyme. Issues #1677, #1678 closed.

## Version Status
- Current main RELEASE_NOTES.md version: `9.0.0` (set in PR #1665 branch; main is also at 9.0.0 from ExceptionIfMissing PR merge)
- Actually main RELEASE_NOTES.md shows `9.0.0 - Feb 26 2026` heading

## Open Repo Assist PRs
- #1676: schema.org microdata for HtmlProvider — clean, awaiting review from dsyme
- #1665: HTTP encoding ISO-8859-1 → UTF-8 — rebased 2026-03-04, awaiting review

## Monthly Activity Issue
- Issue #1684: [Repo Assist] Monthly Activity 2026-03 — updated 2026-03-04

## Notes
- DesignTime signature tests (FSharp.Data.DesignTime.Tests) are pre-existing infrastructure failures
- The full build (RunTests) sometimes fails with OOM (exit code 137) on this runner
- dsyme has been very actively merging PRs - 25+ merged in Feb-Mar 2026
- Issues #1677, #1678, #1679, #1682 are CLOSED
- Issue #1687 (workflow failure) is open but transient — already resolved
- PR #1665 version is 9.0.0 - the encoding change is a breaking change per dsyme
- The RELEASE_NOTES.md conflict pattern: PR #1665 branch and main both have entries under 9.0.0 — need to combine on rebase
