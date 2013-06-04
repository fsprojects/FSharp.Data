#!/bin/bash
#if [ ! -f tools/FAKE/tools/Fake.exe ]; then
#  mono tools/NuGet/NuGet.exe install FAKE -OutputDirectory tools -ExcludeVersion -Prerelease
#fi
#
## Used FAKE to build AssemblyInfo file. Some problems with xbuild and FAKE stop doing more.
#mono tools/FAKE/tools/FAKE.exe build.fsx AssemblyInfo


# Build the project files explicitly because FAKE doesn't run, and Portable etc. can't be built
xbuild  src/FSharp.Data.fsproj   /p:Configuration="Release" 

xbuild  src/FSharp.Data.DesignTime.fsproj   /p:Configuration="Release" 

xbuild  src/FSharp.Data.Experimental.fsproj   /p:Configuration="Release" 

xbuild  src/FSharp.Data.Experimental.DesignTime.fsproj   /p:Configuration="Release" 

xbuild FSharp.Data.Tests.sln /p:Configuration="Release" 


