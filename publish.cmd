:: win-x64
dotnet restore .\CollimationCircles.sln -r win-x64
dotnet publish -c Release -f net6.0 -r win-x64 -o D:\Projekti\Publish\cc\win-64 --self-contained true /p:PublishSingleFile=true /p:PublishReadyToRun=true -p:PublishTrimmed=True -p:TrimMode=CopyUsed

:: linux-x64
::dotnet restore .\CollimationCircles.sln -r linux-x64
::dotnet publish -c Release -f net6.0 -r linux-x64 -o D:\Projekti\Publish\cc\linux-64 --self-contained true /p:PublishSingleFile=true /p:PublishReadyToRun=true

:: linux-arm64
::dotnet restore .\CollimationCircles.sln -r linux-arm64
::dotnet publish -c Release -f net6.0 -r linux-arm64 -o D:\Projekti\Publish\cc\linux-arm64 --self-contained true /p:PublishSingleFile=true /p:PublishReadyToRun=true
