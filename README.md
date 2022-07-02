![Workshop Deployment Status](https://img.shields.io/github/workflow/status/UnhingedIndustries/scripts/Deploy%20scripts%20to%20Steam%20Workshop?label=Workshop%20Deployment&logo=steam&logoColor=lightblue)
![Tests Status](https://img.shields.io/github/workflow/status/UnhingedIndustries/scripts/Run%20all%20tests?label=Tests&logo=csharp&logoColor=lightgreen)
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

Additionally, every script should include meta-data variables `ScriptVersion` and `WorkshopItemId`:

```csharp
const string ScriptVersion = "2.0.7";
const string WorkshopItemId = "2825279640";
```

These will be used to automatically publish the script to Steam Workshop and may be freely used within the script.

### Thumbnail

Every script must have a corresponding `thumb.png` file in the same directory.

The thumbnail file must be a PNG image with width of 636px and height of 358px.

## Automatic publishing

If the script has been modified in any way, it will be automatically published to Steam Workshop after the change is merged to the main branch.

The version is not validated in any way, so it must be manually adjusted before merging the changes.

Thumbnail modification does not trigger automatic publishing of a script.