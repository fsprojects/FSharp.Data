# Repo Assist Memory

## Last Updated
2026-03-01

## Recent Actions
- 2026-03-01: Created PR for doc batch 2 (JsonValue.fs doc comments). Commented on #1679 (transient failure), flagged "Fixes #1678" issue in PR #1681, updated monthly summary #1684.
- 2026-03-01: Three PRs created in earlier runs: #1685 (trimming, closes #1436), #1683 (split HtmlDocument), #1681 (doc comments batch 1).
- 2026-02-28: schema.org microdata+JSON-LD for HtmlProvider (issue #611). PR #1676 created.

## Fix Attempts
- issue #611: schema.org microdata implemented, PR #1676 open
- issue #1251: HTTP encoding change, PR #1665 open
- issue #1436: trimming support, PR #1685 open (just created)
- issue #1678: doc batch 1 PR #1681 open, doc batch 2 (JsonValue.fs) PR pending
- issue #1677: file split batch 1 PR #1683 open

## Monthly Activity Issue
- Issue #1684: [Repo Assist] Monthly Activity 2026-03 — updated this run
- Issue #1682: DUPLICATE of #1684 — needs to be closed next run

## Open Repo Assist PRs Needing Review
- PR (new): JsonValue.fs doc comments (batch 2 of #1678) — just created
- #1685: Trimming support — needs review
- #1683: Split HtmlDocument — needs review
- #1681: Doc comments batch 1 — needs review (WARNING: contains "Fixes #1678")
- #1676: schema.org microdata — needs review
- #1665: HTTP response default encoding — needs review

## Notes
- DesignTime signature tests (FSharp.Data.DesignTime.Tests) are pre-existing infrastructure failures
- The full build (RunTests) sometimes fails with OOM (exit code 137) on this runner
- dsyme has been actively merging PRs - 20+ merged this month
- PR #1681 has "Fixes #1678" in body which would auto-close tracking issue - maintainer warned
- Duplicate monthly issue #1682 needs to be closed (use update_issue next run)
