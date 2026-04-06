# Repo Assist Notes

## Last run: 2026-04-06 (run 24018748563)

### Open PRs
- #1717: HTTP encoding ISO-8859-1 → UTF-8 (Closes #1251; endorsed by dsyme; rebased with TcpListener fix merged from main)
- #1729: perf: rewrite nicePascalName with StringBuilder (removes ToCharArray, lazy seq, intermediate allocs); also removes trimHtml ToCharArray; 2896 tests pass
- Pending (branch repo-assist/test-improve-nameutils-edgecases-2026-04-06): test: add unit tests for trimHtml edge cases, capitalizeFirstLetter, and uniqueGenerator with spaces (13 new tests; 2909 total pass)

### Open Issues  
- #1730: CI format check PR (blocked by workflow permissions; human must apply manually)
- April 2026 Monthly Activity Summary #1726: updated this run
- #1671: Consider System.Text.Json dependency (enhancement; Repo Assist commented previously; no new human activity)

### Recently merged
- #1728: perf: reuse StringBuilder+ResizeArray in CSV parser (merged 2026-04-04)
- #1724: test: TcpListener(0) for free-port selection (merged 2026-04-03)
- #1725: perf: HashSet+String.exists for adorner detection (merged 2026-04-03)
- #1723: fsdocs 22.0.0-alpha.3 + SDK 10.0.201 (merged 2026-04-03)

### Test suite
- 2909 tests as of this run (2896 before + 13 new NameUtils tests)

### Backlog
- Monitor PR #1717 for merge (HTTP encoding fix, endorsed by @dsyme)
- Monitor PR #1729 (nicePascalName/trimHtml perf improvement)
- Check if test PR (nameutils edge cases) got a PR number after run completion
- Consider STJ investigation from #1671
