# Collimation Circles

This program was inspired by Mire De Collimation program written by Gilbert Grillot and Al's Collimation Aid. I combined best features of both and addes some of my own. 

Colimation Circles is developed with .NET 6 and AvaloniaUI Framework, and can run on multiple platforms like Windows, Linux and macOS.

Program was tested on Windows 11 and Linux (Raspberry PI OS Bullseye, Linux Mate). I'm unable to test it on macOS, but it should work.

# Features

- support multiple shapes (circle, cross, screws)
- user interface for managing list of shapes
- scaling up or down of whole setup
- rotating whole setup *
- transparent background
- fully customizable shapes: radius, thickness, color, spacing, rotation, label
- support for profile saving and loading (json files)
- precise position control with keyboard *
- multiple platform support (Windows, Linux, macOS)

** planned for future

![CollimationCircles](https://user-images.githubusercontent.com/7437280/206667602-65872b09-8ba2-4cd2-9bf1-e4a17e17602c.png)

# Instalation and running

- download and install .NET 6 https://dotnet.microsoft.com/en-us/download/dotnet/6.0
- or use Install scripts https://learn.microsoft.com/en-us/dotnet/core/install/linux-scripted-manual#scripted-install

Type following terminal commands:
```
git clone https://github.com/sajmons/CollimationCircles.git
cd ColiminationCircles/ColiminationCircles
dotnet run
```

# Known issues

- Window is not transparent on Raspberry Pi OS Bullseye. Probably other Linux distros too. Unfortunatelly that's makes program unusable on Linux (I hope future versions of AvaloniaUI will fix that)

# Binnary packages

Will be awailable soon
