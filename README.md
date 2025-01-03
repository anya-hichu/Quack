# Quack <img src="https://github.com/anya-hichu/Quack/raw/master/images/icon.png" height="35"/>

Generate and run macro actions quickly using a spotlight inspired interface.

Installable using my custom repository (instructions here: https://github.com/anya-hichu/DalamudPluginRepo) or from compiled archives.

## Tutorial

[Available here](TUTORIAL.md)

Offline PDF versions are published on the release pages.

## Screenshots

### Main (search)
![main](images/main.png)

### Config
#### General
![General config](images/config-general.png)

#### Macros
![Macros config](images/config-macros.png)

#### Schedulers
![Schedulers config](images/config-schedulers.png)

#### Generators
![Generators config](images/config-generators.png)

## Commands

- `/quack main`
- `/quack config`
- `/quack exec [Macro Name or Path]( [Formatting (false/true/format)])?( [Argument Value])*` (double quoting support)
- `[Macro Command]( [Argument Value])*` (double quoting support)
- `/quack cancel` - Cancel all executing macros (/macrocancel is scoped)
