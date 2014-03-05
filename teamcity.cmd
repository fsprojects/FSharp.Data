set FREEBASE_API_KEY=AIzaSyBTcOKmU7L7gFB4AdyAz75JRmdHixdLYjY
call build CleanInternetCaches || exit /B 1
call build All || exit /B 1
call build SourceLink || exit /B 1
call build NuGet || exit /B 1
