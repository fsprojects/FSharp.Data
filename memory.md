# Repo Assist Memory

## Last Updated
2026-02-28

## Recent Actions
- 2026-02-28: Implemented schema.org microdata support for HtmlProvider (issue #611). PR #1676 created, comment posted on issue.
- 2026-02-27: Http.ParseLinkHeader merged (#1675), HTTP Auth docs merged (#1674).
- 2026-02-26: Many PRs merged by dsyme: #1668 (PreferDateTimeOffset), #1666 (remove setup files), #1664 (AppendQueryToUrl), #1663 (UseSchemaTypeNames).
- 2026-02-26: PR #1667 (HttpClient .NET 8+) and PR #1652 (TomlProvider) and PR #1646 (YamlProvider) closed without merge.

## Fix Attempts
- issue #611: schema.org microdata implemented, branch `repo-assist/fix-issue-611-schema-org-microdata`, PR #1676 created 2026-02-28

## Monthly Activity Issue
- Issue #1599: [Repo Assist] Monthly Activity 2026-02 — updated 2026-02-28

## Open Repo Assist PRs Needing Review
- #1676: schema.org microdata for HtmlProvider — needs review
- #1665: HTTP response default encoding (ISO-8859-1 → UTF-8) — needs review

## Notes
- DesignTime signature tests (FSharp.Data.DesignTime.Tests) are pre-existing infrastructure failures, not caused by our changes
- The full build (RunTests) sometimes fails with OOM (exit code 137) on this runner
- dsyme has been actively merging PRs - 20+ merged this month
- Only 6 open issues total as of 2026-02-28 (repo is in very good shape)
- All open issues have already been commented on by Repo Assist
