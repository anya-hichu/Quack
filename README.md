# Quack <img src="https://github.com/anya-hichu/Quack/raw/master/images/icon.png" height="35"/>

Generate and run macro actions quickly using a spotlight inspired interface.

Installable using my custom repository (instructions here: https://github.com/anya-hichu/DalamudPluginRepo) or from compiled archives.

## Tutorial

Markdown version: [click here](TUTORIAL.md)

Published PDF versions are available on the release pages.

## Screenshots

### Main (search)
![main](images/image1.png)

### Config
#### General
![general config](images/image2.png)

#### Macros
![macros config](images/image3.png)

#### Schedulers
![generators config](images/image4.png)

#### Generators
![generators config](images/image5.png)

## Commands

- `/quack main`
- `/quack config`
- `/quack eval ([Line] )+`
- `/quack exec [Macro Name or Path]( [Formatting (false/true/format)])?( [Argument Value])*` 
- `[Macro Command]( [Argument Value])*`
- `/quack cancel` - Cancel all executing macros (/macrocancel is scoped)

Supports single/double quoting with escaping using backslash or doubling character for arguments