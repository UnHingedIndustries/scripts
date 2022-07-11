[![Deployment Status](https://img.shields.io/github/workflow/status/UnhingedIndustries/scripts/Deploy%20scripts?label=Deployment&logo=steam&logoColor=lightblue)](https://github.com/UnHingedIndustries/scripts/actions/workflows/deploy.yml)
[![Tests Status](https://img.shields.io/github/workflow/status/UnhingedIndustries/scripts/Run%20all%20tests?label=Tests&logo=csharp&logoColor=lightgreen)](https://github.com/UnHingedIndustries/scripts/actions/workflows/test.yml)
[![License: WTFPL](https://img.shields.io/badge/License-WTFPL-red.svg)](http://www.wtfpl.net/txt/copying/)

## Local setup

### Space Engineers

For the project to compile, you will need to have [Space Engineers](https://www.spaceengineersgame.com/) installed on your machine.

By default, the Steam installation directory is used to determine shared libraries path.
You may modify it using the `SPACE_ENGINEERS_SHARED_LIBS_PATH` environment variable.

### Dependencies

Project dependencies can be installed with:

```shell
dotnet restore
```

### Running tests

Use the following command to run all tests:

```shell
dotnet test
```

## Anatomy of a script

Every script needs to be placed in a separate namespace because in Space Engineers, every script class is called `Program`.

Additionally, every script should include meta-data variables `ScriptVersion`, `WorkshopItemId` and `ModIoItemId`:

```csharp
const string ScriptVersion = "2.0.7";
const string WorkshopItemId = "2825279640";
const string WorkshopItemId = "2197324";
```

These will be used to automatically publish the script to Steam Workshop and mod.io, and may be freely used within the script.

### Thumbnail

Every script must have a corresponding `thumb.png` file in the same directory.

The thumbnail file must be a PNG image with width of 636px and height of 358px.

### Changelog

The `changelog.txt` file must be overwritten whenever the script is updated.
Contents of this file will be used to generate a log of changes for both Steam Workshop and mod.io.

Each line corresponds to a single changelog entry; empty lines will be ignored.

## Automatic publishing

If the script has been modified in any way, it will be automatically published to Steam Workshop after the change is merged to the main branch.

The version is not validated in any way, so it must be manually adjusted before merging the changes.

Thumbnail modification does not trigger automatic publishing of a script.