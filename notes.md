# Repo Assist Notes

## Last run: 2026-04-29 (run 25091338347)

### Open PRs
- #1758: HTTP encoding ISO-8859-1 to UTF-8 (Closes #1251, 8.1.12 rebase) — awaiting maintainer review/merge
- #1759: 48 unit tests for StringExtensions — awaiting review
- #1762: OpenTelemetry.Api fix (GHSA-g94r-2vxg-569j) — awaiting review
- #1763: FS1182 fix in JsonSchema — awaiting review
- #1764: 39 XmlRuntime unit tests — awaiting review
- #1765: net8.0 multi-target for Html.Core, Http, WorldBank.Core — awaiting review
- #1766: JsonValue.WriteTo InvariantCulture fix + indentation alloc fix — awaiting review
- branch repo-assist/perf-htmlnode-serialize-2026-04-29: HtmlNode.serialize perf improvements
- branch repo-assist/test-htmlnode-tostring-2026-04-29: 25 HtmlNode serialization tests
- #1754: should be closed (superseded by #1758)
- #1717: should be closed (superseded by #1758)

### Open Issues
- #1671: Consider System.Text.Json (no new human activity; commented Feb 2026)
- #1752: Protected files (about PR #1717) — can be closed now #1758 exists
- #1760: NuGet/login v1.2.0 blocked by protected files — needs manual PR
- #1726: April 2026 Monthly Activity Summary (updated this run)

### Infrastructure Note
Pre-existing build failure: OpenTelemetry.Api 1.15.0 vulnerability (GHSA-g94r-2vxg-569j, no patch)
breaks `dotnet run --project build/build.fsproj -t Build`. Does NOT affect GitHub CI.
Tests can be run locally with: dotnet test <project> -p:NuGetAudit=false

### Backlog
1. Monitor all pending Repo Assist PRs for merge
2. Maintainer should close PR #1754 and #1717 (superseded)
3. Maintainer should close issue #1752 (resolved)
4. Continue monitoring #1671 for human activity
