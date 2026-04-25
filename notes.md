# Repo Assist Notes

## Last run: 2026-04-25 (run 24922569845)

### Open PRs
- #1758: HTTP encoding ISO-8859-1 to UTF-8 (Closes #1251, 8.1.12 rebase) — awaiting maintainer review/merge
- #1754: Older HTTP encoding PR (8.1.11) — should be closed (superseded by #1758)
- #1717: Even older HTTP encoding PR — should be closed (superseded by #1758)
- branch repo-assist/eng-nuget-login-v1.2.0-2026-04-25: ci: NuGet/login v1.1.0 → v1.2.0; PR created this run
- branch repo-assist/test-stringextensions-2026-04-25: test: 48 unit tests for StringExtensions; 48 pass; PR created this run

### Open Issues
- #1671: Consider System.Text.Json (no new human activity; commented Feb 2026)
- #1752: Protected files (about PR #1717) — can be closed now #1758 exists
- #1726: April 2026 Monthly Activity Summary (updated this run)

### Infrastructure Note
Pre-existing build failure: OpenTelemetry.Api 1.15.0 vulnerability (GHSA-g94r-2vxg-569j, no patch)
breaks `dotnet run --project build/build.fsproj -t Build`. Transitive via NUnit3TestAdapter 6.1 →
Microsoft.Testing.Platform. Does NOT affect GitHub CI (which uses FAKE build system).

### Backlog
1. Monitor HTTP encoding PR #1758 for merge
2. Maintainer should close PR #1754 and #1717 (superseded)
3. Maintainer should close issue #1752 (resolved)
4. Continue monitoring #1671 for human activity
5. Investigate OpenTelemetry.Api vulnerability fix (no patch available yet)
