# Z64Utils

WIP Tool to parse and view various files and structures in Zelda 64 ROMs.

<img src="https://i.imgur.com/N7ROTdS.png" width=100%/>
Demo (old) : https://youtu.be/AIVDunCtSnM

# Features

## Filesystem Parser
The tool can parse the filesystem contained in a given Zelda ROM and extract/replace/open the files in it.

## ROM/RAM Address Conversion
The tool recreates the memory map of the ROM like how it would lie in virtual RAM (i.e. without any heap allocation / code relocation) and makes you able to convert addresses from one address space to another.

## Object Analyzer
The Object analyzer is capable of analyzing and finding display lists in a given "object" file (these usually contain assets such as model data, textures, skeletons, etc.) and from there, find and decode the data blocks within the object file.

## F3DZEX Disassembler
The tool contains a disassembler that can decode the [F3DZEX](https://wiki.cloudmodding.com/oot/F3DZEX2) commands issued by the game to the RSP.

## Texture Viewer
The texture viewer supports all the texture formats used by the nintendo 64 RDP (CI4, CI8, I4, I8, IA4, IA8, IA16, RGBA16, RGBA32).

## Display List Viewer
The tool contains a renderer that can process [F3DZEX](https://wiki.cloudmodding.com/oot/F3DZEX2) display lists.

## Skeleton Viewer / Animation Player
The Skeleton Viewer can parse and render skeletons and animations used in Ocarina of Time and Majora's Mask.

Note that currently only standart limbs are supported (no SkinLimb/LodLimb)

# Configuration
Z64Utils requires some configuration files in order to work properly. The purpose of these files is to give some basic information on the different ROM versions.

You can find these configuration files as well as some additional information about them [here](https://github.com/zeldaret/Z64Utils/tree/config).

# Dependencies

Currently, the only requirement is `.NET 6` (Not to be confused with `.NET Core` or `.NET Framework`).

## Windows

The general purpose download link for `.NET` is [this](https://dotnet.microsoft.com/download), and the direct download is [here](https://builds.dotnet.microsoft.com/dotnet/WindowsDesktop/6.0.36/windowsdesktop-runtime-6.0.36-win-x64.exe).

## Linux

On Ubuntu, you can install `.NET 6` with `sudo apt install dotnet-runtime-6.0`.

For other Linux distributions, additional details or other installation methods, see (Microsoft's documentation)[https://learn.microsoft.com/en-us/dotnet/core/install/linux].

# Contributing

This repository uses the [CSharpier](https://csharpier.com/) code formatter. Install it with `dotnet tool install csharpier --version 0.30.6` and run it with `dotnet csharpier .`.

# Releasing

To make a new release:

1. Bump the version number in the csproj.
2. Create a release and associated tag on GitHub.
3. GitHub actions will automatically build Z64Utils with `--version-suffix ''` and upload the binaries to the release.
