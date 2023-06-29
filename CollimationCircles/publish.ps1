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
	
    $command = "dotnet publish -c Release -f $Framework -r $Runtime -o $Output/$Runtime --self-contained true /p:PublishSingleFile=true /p:PublishReadyToRun=true /p:DebugType=None /p:DebugSymbols=false"

	Write-Host $command -ForegroundColor green
    Invoke-Expression $command

	Compress-Archive -Force $Output/$Runtime/** $Output/$Index-$appName-$version-$Runtime.zip
	Remove-Item –path $Output/$Runtime –Recurse -Force
}

$Index = 0;
foreach ($Runtime in $Runtimes)
{
	$Index++
    PublishOne $Runtime
}