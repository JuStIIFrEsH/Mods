# FreshCommands

A small, standalone BepInEx mod for custom Valheim console commands. It does not require Jotunn.

## Commands

- `corpse tp` — teleports the character to their most recently created tombstone.
- `corpse recover` — performs the normal tombstone interaction, collecting its contents when they fit or opening the standard inventory when they do not.
- `tp home` — teleports the character to their current spawn point.
- `set <name>` — adds the current player position to saved locations. For example, `/set locationX` then `/tp locationX`.
- `set <name> <x> <z>` — adds supplied X/Z coordinates to saved locations; for example, `/set locationX 1250 -800`.
- `delete <name>` — permanently removes a saved location from the world save.
- `tp <name>` — teleports to a saved location. `/tp` alone lists all saved locations and their coordinates.
- `freshcommands` — lists every available FreshCommands command.

All commands also work from in-game chat when prefixed with `/`, such as `/corpse tp` or `/tp home`.

Saved locations are shared by everyone on the same world and persist in that world's database save data.

Commands work in single-player and for the hosting player. Dedicated-server and remote-client command routing is not included.

## Installation

Install directly with Thunderstore, or copy `FreshCommands.dll` into a folder beneath `BepInEx/plugins`, then launch Valheim. Use the console directly (`/corpse tp`) or in-game chat with a `/` prefix (`corpse tp`). Dev commands do not need to be enabled.

## Requirements

- Valheim
- BepInEx 5
