# Repo Assist Notes

## Last run: 2026-04-09 (run 24172391552)

### Open PRs
- #1717: HTTP encoding ISO-8859-1 → UTF-8 (Closes #1251; endorsed by dsyme; all CI checks pass; ready for merge)
- #1734: test: JsonValue.ParseMultiple, Load(Stream), Load(TextReader), WriteTo (11 new tests; 2920 total pass)
- #1735: perf: avoid ToCharArray allocations in TextConversions, HtmlParser, HtmlCssSelectors, HtmlOperations (2909 tests pass)

### Open Issues  
- #1730: CI format check issue (can be closed now - PR #1732 was merged)
- April 2026 Monthly Activity Summary #1726: updated this run
- #1671: Consider System.Text.Json dependency (enhancement; Repo Assist commented previously; no new human activity)

### Recently merged (2026-04-07 by @dsyme)
- #1733: perf: avoid ToCharArray allocations in HtmlParser (merged)
- #1732: ci: add Fantomas format check job to PR workflow (merged)
- #1731: test: add unit tests for trimHtml edge cases, capitalizeFirstLetter, uniqueGenerator (merged)
- #1729: perf: nicePascalName StringBuilder rewrite; trimHtml ToCharArray removed (merged)
- #1728: perf: reuse StringBuilder+ResizeArray in CSV parser (merged 2026-04-04)
- #1725: perf: HashSet+String.exists for adorner detection (merged 2026-04-03)
- #1724: test: TcpListener(0) for free-port selection (merged 2026-04-03)
- #1723: fsdocs 22.0.0-alpha.3 + SDK 10.0.201 (merged 2026-04-03)

### Test suite
- 2909 tests as of this run (on main; PR #1734 has 2920)

### Backlog
- Monitor PR #1717 for merge (HTTP encoding fix, endorsed by @dsyme, all CI ✅)
- Monitor PR #1734 (JsonValue tests)
- Monitor PR #1735 (ToCharArray perf: TextConversions, HtmlParser, HtmlCssSelectors, HtmlOperations)
- Close issue #1730 (CI format check - now resolved)
- Consider STJ investigation from #1671
