# Z64Utils_Avalonia

WIP port of [Z64Utils](https://github.com/zeldaret/Z64Utils) from Windows Forms to [Avalonia](https://github.com/AvaloniaUI/Avalonia), in order to make it a cross-platform application, notably enabling running on Linux.

Based on Z64Utils revision [a960313b7cf6631536d1d20e5f1fdc89259687f1](https://github.com/zeldaret/Z64Utils/tree/a960313b7cf6631536d1d20e5f1fdc89259687f1). The plan is to upstream changes eventually after reaching feature parity with zeldaret/Z64Utils.

# Installing

A github action builds binaries. You need a .NET 6 (6+ ?) runtime.

## Linux

Install the .NET 6 runtime:

```shell
add-apt-repository ppa:dotnet/backports
apt install dotnet-runtime-6.0
```

Or it may be possible to install the latest runtime without having to add the backports repository, `apt install dotnet-runtime-8.0` currently, but I haven't confirmed a .NET 8 runtime can run a .NET 6 -targeting application (this says it can: https://learn.microsoft.com/en-us/dotnet/framework/migration-guide/version-compatibility )

If the runtime isn't found though it's installed, maybe try https://stackoverflow.com/a/73315318

# CLI

Run Z64Utils with arguments to directly open something.

Examples:

```shell
./Z64Utils --help
./Z64Utils --rom oot-gc-eu-mq-dbg.z64
./Z64Utils --rom oot-gc-eu-mq-dbg.z64 --object-analyzer object_box
./Z64Utils --rom oot-gc-eu-mq-dbg.z64 --object-analyzer object_box --dlist-viewer dlist_000006F0
./Z64Utils --rom oot-gc-eu-mq-dbg.z64 --object-analyzer object_daiku --skeleton-viewer skel_00007958
```

# Building

## Ubuntu 24.04:

https://learn.microsoft.com/en-us/dotnet/core/install/linux-ubuntu#ubuntu-net-backports-package-repository

```shell
add-apt-repository ppa:dotnet/backports
apt install dotnet-sdk-6.0
dotnet build
```

Installing dotnet-sdk-8.0 to compile for .NET 6 may be possible too? GitHub (action) does it.

See also: the GitHub Actions workflow file under .github/

## Version data

Copying versions isn't part of csproj because I haven't figure out what to do with versions data yet

If using vs code, you can change the debug launch config with `"preLaunchTask": "build_and_cp_versions",` and create task build_and_cp_versions as:

```json
{
    "label": "build_and_cp_versions",
    "dependsOn": "build",
    "command": "cp -r -t bin/Debug/net6.0 Z64Utils-Config-master/versions",
    "type": "shell",
}
```
