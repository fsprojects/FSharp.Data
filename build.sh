#!/bin/bash
if test "$OS" = "Windows_NT"
then
  # use .Net

  .paket/paket.exe restore
  exit_code=$?
  if [ $exit_code -ne 0 ]; then
  	exit $exit_code
  fi

  packages/FAKE/tools/FAKE.exe $@ --fsiargs build.fsx 
else
  # On Linux (or at least, Ubuntu), update the libunwind8 package so .NET Core can run, see https://github.com/dotnet/cli/issues/3390
  if [ $(uname -s) = 'Linux' ]; then
      sudo apt-get install libunwind8
  fi

  mono .paket/paket.exe restore
  exit_code=$?
  if [ $exit_code -ne 0 ]; then
  	exit $exit_code
  fi

  mono --runtime=v4.0 packages/FAKE/tools/FAKE.exe build.fsx -d:MONO $@
fi
