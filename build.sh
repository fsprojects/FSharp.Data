#!/bin/bash
if [ ! -f packages/FAKE/tools/Fake.exe ]; then
  mono --runtime=v4.0 .NuGet/NuGet.exe install FAKE -OutputDirectory packages -ExcludeVersion -Prerelease
fi
#if [ ! -f packages/SourceLink.Fake/tools/Fake.fsx ]; then
#  mono --runtime=v4.0 .NuGet/NuGet.exe install SourceLink.Fake -OutputDirectory packages -ExcludeVersion
#fi
mono --runtime=v4.0 packages/FAKE/tools/FAKE.exe build.fsx -d:MONO $@
