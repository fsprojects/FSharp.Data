# Repo Assist Notes

## Last Updated
2026-03-06

## Recent Actions
- 2026-03-06: Updated PR #1665 — merged main into branch resolving RELEASE_NOTES.md conflict. Version stays at 9.0.0 with HTTP encoding entry as first item.
- 2026-03-05: Updated PR #1665 — pushed RELEASE_NOTES.md fix: added ExceptionIfMissing + trimming entries to 9.0.0 section. Commented on #1687 confirming resolved.
- 2026-03-03: Rebased PR #1665 onto main (resolved RELEASE_NOTES.md conflict). Updated monthly summary #1684.

## Version Status
- Current main RELEASE_NOTES.md version: `8.1.0-beta` (unreleased)
- PR #1665 branch version: `9.0.0` (encoding change is a breaking change per dsyme)

## Open Repo Assist PRs
- #1676: schema.org microdata for HtmlProvider — mergeable_state: clean, awaiting review
- #1665: HTTP encoding ISO-8859-1 → UTF-8 — merged main 2026-03-06 to resolve conflict

## Monthly Activity Issue
- Issue #1684: [Repo Assist] Monthly Activity 2026-03 — updated 2026-03-06

## Notes
- DesignTime signature tests (FSharp.Data.DesignTime.Tests) are pre-existing infrastructure failures
- The full build (RunTests) sometimes fails with OOM (exit code 137) on this runner
- dsyme has been very actively merging PRs - 25+ merged in Feb-Mar 2026
- PR #1688 by Copilot SWE Agent is a draft addressing #1687 — redundant since ExceptionIfMissing already merged
- Issue #1687 (workflow failure) was resolved in 2026-03-05 run; expires Mar 9 2026
