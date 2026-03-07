# Repo Assist Notes

## Last Updated
2026-03-07

## Recent Actions
- 2026-03-07: Task 6 - checked PRs #1676 and #1665, both have passing CI, no conflicts.
- 2026-03-07: Task 4 - created CI improvement PR (add NuGet cache to push-master.yml).
- 2026-03-06: Updated PR #1665 — merged main into branch resolving RELEASE_NOTES.md conflict. Version stays at 9.0.0 with HTTP encoding entry as first item.

## Version Status
- Current main RELEASE_NOTES.md version: `8.1.0-beta` (unreleased)
- PR #1665 branch version: `9.0.0` (encoding change is a breaking change per dsyme)

## Open Repo Assist PRs
- #1676: schema.org microdata for HtmlProvider — CI passing, awaiting review
- #1665: HTTP encoding ISO-8859-1 → UTF-8 — CI passing, main is 1 commit ahead
- (new): CI cache for push-master.yml — created 2026-03-07

## Monthly Activity Issue
- Issue #1684: [Repo Assist] Monthly Activity 2026-03 — updated 2026-03-07

## Notes
- DesignTime signature tests (FSharp.Data.DesignTime.Tests) are pre-existing infrastructure failures
- The full build (RunTests) sometimes fails with OOM (exit code 137) on this runner
- dsyme has been very actively merging PRs - 25+ merged in Feb-Mar 2026
- Issue #1687 (workflow failure) still open but confirmed resolved, expires Mar 9 2026
- PR #1688 by Copilot SWE Agent is a non-Repo-Assist PR for ExceptionIfMissing
