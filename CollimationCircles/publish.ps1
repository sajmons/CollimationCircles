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
            Run dotnet publish for project, create ZIP archive and remove temporary files.
    #>	
    
	$appName = [System.IO.Path]::GetFileNameWithoutExtension($Project)

	$xml = [Xml] (Get-Content $Project)
	$version = [Version] $xml.Project.PropertyGroup.Version
	
	Write-Host "dotnet publish -c Release -f $Framework -r $Runtime -o $Output/$Runtime --self-contained true /p:PublishSingleFile=true /p:PublishReadyToRun=true" -ForegroundColor green

	dotnet publish -c Release -f $Framework -r $Runtime -o $Output/$Runtime --self-contained true /p:PublishSingleFile=true /p:PublishReadyToRun=true
	Compress-Archive -Force $Output/$Runtime/** $Output/$appName-$version-$Runtime.zip
	Remove-Item –path $Output/$Runtime –Recurse -Force
}

foreach ($Runtime in $Runtimes)
{
    PublishOne($Runtime)
}