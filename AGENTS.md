# Coding Agent Guidelines

## Testing

Add comprehensive tests for all new features. Tests are located in `tests/`:

- `FSharp.Data.Core.Tests/` - Core functionality tests (CSV, JSON, HTML, XML parsers)
- `FSharp.Data.DesignTime.Tests/` - Type provider design-time tests
- `FSharp.Data.Tests/` - Integration and end-to-end tests
- `FSharp.Data.Reference.Tests/` - Reference/signature tests

Match test style and naming conventions of existing tests in the appropriate project.

## Code Formatting

Run Fantomas before committing:

```bash
dotnet run --project build/build.fsproj -t Format
```

To check formatting without modifying files:

```bash
dotnet run --project build/build.fsproj -t CheckFormat
```

## Build and Test

Build the solution:

```bash
./build.sh
# or
dotnet run --project build/build.fsproj -t Build
```

Run all tests:

```bash
dotnet run --project build/build.fsproj -t RunTests
```

Run everything (build, test, docs, pack):

```bash
dotnet run --project build/build.fsproj -t All
```

## Documentation

Documentation lives in `docs/`. Update relevant docs when adding features:

- `docs/library/` - API and provider documentation
- `docs/tutorials/` - Tutorial content

Generate and preview docs:

```bash
dotnet run --project build/build.fsproj -t GenerateDocs
```

## Release Notes

Update `RELEASE_NOTES.md` at the top of the file for any user-facing changes. Follow the existing format:

```markdown
## X.Y.Z - Date

- Description of change by @author in #PR
```

You should bump the version number in `RELEASE_NOTES.md` and `Directory.Build.props` for the next release - choose a new version number following semantic versioning guidelines. Assume that any listed versions have already been released.