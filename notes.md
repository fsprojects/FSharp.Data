# Repo Assist Memory

## Last Updated
2026-02-27

## Recent Actions
- 2026-02-27: Implemented Http.ParseLinkHeader for RFC 5988 Link header parsing (issue #805). PR created, comment posted.
- 2026-02-27: Commented on #1671 (System.Text.Json dependency) with analysis.
- 2026-02-26: Implemented schema.org microdata support for HtmlProvider (issue #611). PR created on branch `repo-assist/fix-issue-611-schemaorg-htmlprovider`. Comment posted on issue #611.

## Fix Attempts
- issue #805: Http.ParseLinkHeader implemented, branch `repo-assist/fix-issue-805-http-parse-link-header` — PR created 2026-02-27
- issue #611: Implemented in branch `repo-assist/fix-issue-611-schemaorg-htmlprovider` — PR created 2026-02-26

## Monthly Activity Issue
- Issue #1599: [Repo Assist] Monthly Activity 2026-02 — updated 2026-02-27

## Open Repo Assist PRs Needing Review
- (new, unnumbered): Http.ParseLinkHeader — just created
- #1674: HTTP Auth docs — needs review
- #1668: Add PreferDateTimeOffset — needs review
- #1667: Use HttpClient on .NET 8+ — needs review
- #1666: Remove FSI testing setup files — needs review
- #1665: Change HTTP response default encoding — needs review
- #1664: Make AppendQueryToUrl public — needs review
- #1663: Add UseSchemaTypeNames to XmlProvider — needs review
- #1652: Add TomlProvider — needs review
- #1646: Add YamlProvider — needs review

## Notes
- DesignTime signature tests (FSharp.Data.DesignTime.Tests) are pre-existing infrastructure failures, not caused by our changes
- The full build (RunTests) sometimes fails with OOM (exit code 137) on this runner
- dsyme agrees that issue #1241 (missing fields return empty string) is not good enough; related to System.Text.Json (#1671) discussion
