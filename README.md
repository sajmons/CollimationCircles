# Collimation Circles

This program was inspired by Mire De Collimation program written by Gilbert Grillot and Al's Collimation Aid. I combined best features of both and addes some of my own. 

Colimation Circles is developed with .NET 6 and AvaloniaUI Framework, and can run on multiple platforms like Windows, Linux and macOS.

Program was tested on Windows 11 and Linux (Raspberry PI OS Bullseye, Linux Mate). I'm unable to test it on macOS, but it should work.

# Features

- support multiple shapes (circle, cross, screws)
- user interface for managing list of shapes
- scaling up or down of whole setup
- rotation of whole setup
- transparent background
- fully customizable shapes: radius, thickness, color, spacing, rotation, label
- support for profile saving and loading (json files)
- precise position control with keyboard
- multiple platform support (Windows, Linux, macOS)

![image](https://user-images.githubusercontent.com/7437280/207387640-f0b2f880-c2d1-4462-a083-bab68d465b8d.png)

# Instalation and running

- download and install latest .NET Framework https://dotnet.microsoft.com/en-us/download/dotnet
- or use Install scripts https://learn.microsoft.com/en-us/dotnet/core/install/linux-scripted-manual#scripted-install
- Raspberry Pi https://learn.microsoft.com/en-us/dotnet/iot/deployment

Type following terminal commands:
```
git clone https://github.com/sajmons/CollimationCircles.git
cd ColiminationCircles/ColiminationCircles
dotnet run
```

# Building

For more on building see https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-publish.

## Linux ARM 64
```
dotnet publish --runtime linux-arm64 /p:PublishSingleFile=true --output ~/Home/CollimationCircle
cd ~/Home/CollimationCircle
./CollimationCircle
```

# Collimation process

I recomend you to read excelect [AstroBaby Collimation tutorial](https://www.astro-baby.com/astrobaby/help/collimation-guide-newtonian-reflector/). Images below are generated following procedure described in the tutorial.

## Centering secondary mirror under focuser

![image](https://user-images.githubusercontent.com/7437280/207791142-3c5f99d5-98b9-4dd0-92c8-32a19a7d9906.png)

In this stage our goal is to center secondary mirror to center of focuser tube.


# Known issues

- Window is not transparent on Raspberry Pi OS Bullseye. Probably other Linux distros too. Unfortunatelly that's makes program unusable on Linux (I hope future versions of AvaloniaUI will fix that)
