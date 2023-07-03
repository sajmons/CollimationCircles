[CmdletBinding()]
Param(
	# Project
    [Parameter(Mandatory=$True)]
    [ValidateNotNullOrEmpty()]
    [Alias("p")]
    [string]$Project,
    # Runtime
    [Parameter(Mandatory=$False)]
    [ValidateNotNullOrEmpty()]
	[ValidateSet("win-x64","linux-x64","linux-arm64","osx-x64")]
    [Alias("r")]
    [string[]]$Runtimes = @("win-x64","linux-x64","linux-arm64","osx-x64"),
    # Framework
    [Parameter(Mandatory=$False)]
    [ValidateNotNullOrEmpty()]
    [Alias("f")]
    [string]$Framework = "net7.0",
	# Output
    [Parameter(Mandatory=$False)]
    [ValidateNotNullOrEmpty()]
    [Alias("o")]
    [string]$Output = "./publish"
)

Function PublishOne($Runtime)
{
    <#
        .SYNOPSIS
            Run dotnet publish for project, get version from csproj file, create ZIP archive and remove temporary files.
    #>	
    
	$appName = [System.IO.Path]::GetFileNameWithoutExtension($Project)

	$xml = [Xml] (Get-Content $Project)
	$version = [Version] $xml.Project.PropertyGroup.Version
	
    $commandRestore = "dotnet restore -r $Runtime"

    Write-Host $commandRestore -ForegroundColor green
    Invoke-Expression $commandRestore

    $commandPublish = "dotnet publish -c Release -f $Framework -r $Runtime -o $Output/$Runtime --self-contained true /p:PublishSingleFile=true /p:PublishReadyToRun=true /p:DebugType=None /p:DebugSymbols=false"

	Write-Host $commandPublish -ForegroundColor green
    Invoke-Expression $commandPublish

    if ($Runtime -eq "osx-x64")
    {
        # make bundle for macosx
        $commandBundle = "dotnet msbuild -t:BundleApp -p:RuntimeIdentifier=$Runtime -p:OutputPath=$Output/$Runtime/"
        Write-Host $commandBundle -ForegroundColor green
        Invoke-Expression $commandBundle
    }

    # To maintain backward compatibility for downloading new version GitHub release files must be ordered so that win-x64 is the first file.
    # That's why I aded number infront of file name to maintain correct order.
    # You must always specify win-x64 as first runtime in $runtimes list
	Compress-Archive -Force $Output/$Runtime/** $Output/$Index-$appName-$version-$Runtime.zip
	#Remove-Item –path $Output/$Runtime –Recurse -Force
}

$Index = 0;
foreach ($Runtime in $Runtimes)
{
	$Index++ # for github file ordering
    PublishOne $Runtime
}