@pushd "%~dp0\"
dotnet run -f net10.0 -c release -- --runtimes net10.0 %*
@popd
