#!/bin/bash
if [ ! -f tools/FAKE/tools/Fake.exe ]; then
  mono .NuGet/NuGet.exe install FAKE -OutputDirectory tools -ExcludeVersion -Prerelease
fi
mono tools/FAKE/tools/FAKE.exe build.fsx $@
