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

Function CreateInfoPlistFile
{ 
    Param
    (        
        [Parameter(Mandatory=$true, Position=0)]
        [string] $AppName,
        [Parameter(Mandatory=$true, Position=1)]
        [string] $AppVersion,
        [Parameter(Mandatory=$true, Position=2)]
        [string] $Bundle
    )

    # Create & Set The Formatting with XmlWriterSettings class
    $xmlObjectsettings = New-Object System.Xml.XmlWriterSettings
    #Indent: Gets or sets a value indicating whether to indent elements.
    $xmlObjectsettings.Indent = $true
    #Gets or sets the character string to use when indenting. This setting is used when the Indent property is set to true.
    $xmlObjectsettings.IndentChars = "    "
 
    # Set the File path & Create The Document
    $XmlFilePath = "$Output/$Bundle/Contents/Info.plist"
    $XmlObjectWriter = [System.XML.XmlWriter]::Create($XmlFilePath, $xmlObjectsettings)
 
    # Write the XML declaration and set the XSL
    $XmlObjectWriter.WriteStartDocument()

    $m = $XmlObjectWriter.gettype().getmethod("WriteDocType")
    $m.Invoke($XmlObjectWriter, @("plist", "-//Apple//DTD PLIST 1.0//EN", "http://www.apple.com/DTDs/PropertyList-1.0.dtd", $null))

    #<?xml version="1.0" encoding="UTF-8"?>
    #<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
    #<plist version="1.0">
    #<dict>
    #    <key>CFBundleIconFile</key>
    #    <string>myicon-logo.icns</string>
    #    <key>CFBundleIdentifier</key>
    #    <string>com.identifier</string>
    #    <key>CFBundleName</key>
    #    <string>MyApp</string>
    #    <key>CFBundleVersion</key>
    #    <string>1.0.0</string>
    #    <key>LSMinimumSystemVersion</key>
    #    <string>10.12</string>
    #    <key>CFBundleExecutable</key>
    #    <string>MyApp.Avalonia</string>
    #    <key>CFBundleInfoDictionaryVersion</key>
    #    <string>6.0</string>
    #    <key>CFBundlePackageType</key>
    #    <string>APPL</string>
    #    <key>CFBundleShortVersionString</key>
    #    <string>1.0</string>
    #    <key>NSHighResolutionCapable</key>
    #    <true/>
    #</dict>
    #</plist>     

    $XmlObjectWriter.WriteStartElement("plist") # <-- Start plist
    $XmlObjectWriter.WriteAttributeString("version", "1.0");    

        $XmlObjectWriter.WriteStartElement("dict") # <-- Start dict
         
            $XmlObjectWriter.WriteElementString("key","CFBundleIconFile")
            $XmlObjectWriter.WriteElementString("string", "icon.icns")

            $XmlObjectWriter.WriteElementString("key","CFBundleIdentifier")
            $XmlObjectWriter.WriteElementString("string", "com.saimons.$AppName")

            $XmlObjectWriter.WriteElementString("key", "CFBundleName")
            $XmlObjectWriter.WriteElementString("string", $AppName)

            $XmlObjectWriter.WriteElementString("key", "CFBundleVersion")
            $XmlObjectWriter.WriteElementString("string", $AppVersion)

            $XmlObjectWriter.WriteElementString("key", "LSMinimumSystemVersion")
            $XmlObjectWriter.WriteElementString("string", "12")

            $XmlObjectWriter.WriteElementString("key", "CFBundleExecutable")
            $XmlObjectWriter.WriteElementString("string", $Appname)

            $XmlObjectWriter.WriteElementString("key", "CFBundleInfoDictionaryVersion")
            $XmlObjectWriter.WriteElementString("string", "6.0")

            $XmlObjectWriter.WriteElementString("key", "CFBundlePackageType")
            $XmlObjectWriter.WriteElementString("string", "APPL")

            $XmlObjectWriter.WriteElementString("key", "CFBundleShortVersionString")
            $XmlObjectWriter.WriteElementString("string", $AppVersion)

            $XmlObjectWriter.WriteElementString("key", "NSHighResolutionCapable")
            $XmlObjectWriter.WriteElementString("true", "")        
 
        $XmlObjectWriter.WriteEndElement() # <-- End dict
 
    $XmlObjectWriter.WriteEndElement();    # End plist
 
    # Finally close the XML Document
    $XmlObjectWriter.WriteEndDocument()
    $XmlObjectWriter.Flush()
    $XmlObjectWriter.Close()
}

Function MakeMacOSPackage
{
    Param
    (
         [Parameter(Mandatory=$true, Position=0)]
         [string] $AppName,
         [Parameter(Mandatory=$true, Position=1)]
         [string] $AppVersion,
         [Parameter(Mandatory=$true, Position=2)]
         [string] $Runtime
    )

    $bundle = $AppName + ".app"

    # create bundle directory structure
    New-Item -Path $Output/$bundle -ItemType Directory
    New-Item -Path $Output/$bundle/Contents -ItemType Directory
    New-Item -Path $Output/$bundle/Contents/MacOS -ItemType Directory
    New-Item -Path $Output/$bundle/Contents/Resources -ItemType Directory

    CreateInfoPlistFile $AppName $AppVersion $bundle

    # copy applicaton files to Contents/MacOS folder
    Copy-Item -Path $Output/$Runtime/** -Destination $Output/$bundle/Contents/MacOS    
  
    # copy icon to Contents/Resources folder
    Move-Item –Path $Output/$bundle/Contents/MacOS/icon.icns -Destination $Output/$bundle/Contents/Resources
}

Function GrantExecutablePermissions()
{
    Param
    (
         [Parameter(Mandatory=$true, Position=0)]
         [string] $AppName,
         [Parameter(Mandatory=$true, Position=1)]
         [string] $Output,
         [Parameter(Mandatory=$true, Position=2)]
         [string] $Runtime
    )

    # Grant Read and Execute Access of a specific file
    # ICACLS $Output/$Runtime /grant:r "users:(RX)" /C   
    
    $commandGrantPermissions = "icacls $Output/$Runtime/$AppName /grant:r Users:RX /C"

    Write-Host $commandGrantPermissions -ForegroundColor green

    Invoke-Expression $commandGrantPermissions
}

Function PublishOne
{
    Param
    (
         [Parameter(Mandatory=$true, Position=0)]
         [string] $Runtime         
    )
    
	$appName = [System.IO.Path]::GetFileNameWithoutExtension($Project)

	$xml = [Xml] (Get-Content $Project)
	$version = [Version] $xml.Project.PropertyGroup.Version
	
    $commandRestore = "dotnet restore -r $Runtime"

    Write-Host $commandRestore -ForegroundColor green
    Invoke-Expression $commandRestore

    $commandPublish = "dotnet publish -c Release -f $Framework -r $Runtime -o $Output/$Runtime --self-contained true /p:PublishSingleFile=true /p:PublishReadyToRun=true /p:DebugType=None /p:DebugSymbols=false -p:PublishTrimmed=true -p:TrimMode=partial"

    if ($Runtime -eq "osx-x64")
    {
        $commandPublish += " /p:UseAppHost=true"
    }

	Write-Host $commandPublish -ForegroundColor green
    Invoke-Expression $commandPublish

    $outputDir = "$Output/$Runtime/**"

    if ($Runtime -eq "osx-x64")
    {
        # make bundle for macosx
        MakeMacOSPackage $appName $version $Runtime
        $outputDir = "$Output/$appName" + ".app"
    }

    GrantExecutablePermissions $appName $Output $Runtime

    # To maintain backward compatibility for downloading new version GitHub release files must be ordered so that win-x64 is the first file.
    # That's why I aded number infront of file name to maintain correct order.
    # You must always specify win-x64 as first runtime in $runtimes list
	Compress-Archive -Force $outputDir $Output/$Index-$appName-$version-$Runtime.zip

    if ($Runtime -eq "osx-x64")
    {
        Remove-Item –path $outputDir –Recurse -Force
    }
    
	Remove-Item –path $Output/$Runtime –Recurse -Force    
}

$Index = 0;
foreach ($Runtime in $Runtimes)
{
	$Index++ # for github file ordering
    PublishOne $Runtime
}