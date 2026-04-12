# Repo Assist Notes

## Last run: 2026-04-12 (run 24298725681)

### SYSTEMIC ISSUE: MCP servers blocked by policy

Root cause: Copilot CLI 1.0.24 performs an MCP registry policy check at
api.github.com/copilot/mcp_registry before loading MCP servers.
This request fails with 401 Bad credentials because no GitHub token
is configured for this endpoint. As a result, ALL non-default MCP servers
(github, safeoutputs) are blocked.

This has caused runs 24226524583, 24274612549, and 24298725681 to fail
with "No Safe Outputs Generated".

Fix needed: Provide a GitHub token with copilot write scope to the
copilot CLI environment, OR configure the workflow to allow these MCPs
without policy check (if supported).

### Prepared but unpushed perf work
Branch: repo-assist/perf-css-readstring-json-asspan-2026-04-12
Cannot push without safeoutputs tools. Changes:
- HtmlCssSelectors.fs: readString now uses StringBuilder (O(n2) to O(n) allocations)
- JsonValue.fs: JsonStringEncodeTo uses AsSpan on .NET 6+ (avoids Substring allocs)
Build and tests pass (format ok, all tests green).

### Open PRs (all CI green)
- #1717: HTTP encoding ISO-8859-1 to UTF-8 (endorsed by dsyme; all CI pass; ready for merge)
- #1734: test: JsonValue.ParseMultiple, Load(Stream), Load(TextReader), WriteTo
- #1735: perf: avoid ToCharArray allocations in TextConversions, HtmlParser, etc.

### Open Issues
- #1671: Consider System.Text.Json dependency (no new human activity)
- #1726: April 2026 Monthly Activity Summary (needs update when MCP fixed)
- #1736: Repo Assist failed (documents MCP failures)

### Recently merged (by dsyme)
- #1733: perf: avoid ToCharArray allocations in HtmlParser (merged 2026-04-07)
- #1732: ci: add Fantomas format check job to PR workflow (merged 2026-04-05)
- #1731: test: trimHtml, capitalizeFirstLetter, uniqueGenerator tests (merged 2026-04-06)
- #1729: perf: nicePascalName StringBuilder rewrite (merged 2026-04-05)
- #1728: perf: reuse StringBuilder+ResizeArray in CSV parser (merged 2026-04-04)

### Backlog (when MCP is fixed)
1. Push branch repo-assist/perf-css-readstring-json-asspan-2026-04-12 and create PR
2. Update monthly summary #1726 with this run activity
3. Add comment to issue #1736 noting root cause diagnosis
4. Monitor PR #1717 for merge
