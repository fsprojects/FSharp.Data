call build CleanInternetCaches || exit /B 1
call build All || exit /B 1
call build SourceLink || exit /B 1
call build NuGet || exit /B 1
