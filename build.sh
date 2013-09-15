#!/bin/bash
#if [ ! -f tools/FAKE/tools/Fake.exe ]; then
#  mono tools/NuGet/NuGet.exe install FAKE -OutputDirectory tools -ExcludeVersion -Prerelease
#fi
#
## Used FAKE to build AssemblyInfo file. Some problems with xbuild and FAKE stop doing more.
#mono tools/FAKE/tools/FAKE.exe build.fsx AssemblyInfo

xbuild FSharp.Data.sln /p:Configuration="Release" 
xbuild FSharp.Data.Tests.sln /p:Configuration="Release" 

# Note, NUnit.Runners 2.6.2 has a bug related to Mono, see https://bugs.launchpad.net/nunitv2/+bug/1076932. Version 2.6.1 fares better
mono tools/NuGet/NuGet.exe install NUnit.Runners -Version 2.6.1

# Note, these main tests are running fairly cleanly on Mac/Linux when last checked
mono ./NUnit.Runners.2.6.1/tools/nunit-console.exe tests/FSharp.Data.Tests/bin/Release/FSharp.Data.Tests.dll -result=TestResult.FSharp.Data.Tests.xml

# Note, these tests are NOT running cleanly on Mac/Linux when last checked
# mono ./NUnit.Runners.2.6.1/tools/nunit-console.exe tests/FSharp.Data.Tests.DesignTime/bin/Release/FSharp.Data.Tests.DesignTime.dll  -result=TestResult.FSharp.Data.Tests.DesignTime.xml

# Note, these tests are NOT running cleanly on Mac/Linux when last checked
# mono ./NUnit.Runners.2.6.1/tools/nunit-console.exe  tests/FSharp.Data.Tests.Experimental.DesignTime/bin/Release/FSharp.Data.Tests.Experimental.DesignTime.dll -result=TestResult.FSharp.Data.Tests.Experimental.DesignTime.xml
