call build CleanInternetCaches || exit /B %ERRORLEVEL%
call build All || exit /B %ERRORLEVEL%
call build SourceLink || exit /B %ERRORLEVEL%
call build NuGet || exit /B %ERRORLEVEL%
