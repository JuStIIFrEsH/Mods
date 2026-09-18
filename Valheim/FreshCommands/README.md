# FreshCommands

A lightweight standalone BepInEx mod that adds useful console and chat commands to Valheim.

**Author:** JuStIIFrEsH

## Links

- [Valheim Mods](https://justiifresh.com/valheim/)
- [Report a Bug](https://justiifresh.com/valheim/bug/)
- [Thunderstore](https://thunderstore.io/c/valheim/p/JuStIIFrEsH/FreshCommands/)

## Requirements

- BepInEx 5

## Installation

Install FreshCommands through Thunderstore, or copy `FreshCommands.dll` into a folder under `BepInEx/plugins` and launch Valheim.

Dev commands do not need to be enabled.

## Commands

- `corpse tp` — teleports the character to their most recently created tombstone.
- `corpse recover` — performs the normal tombstone interaction, collecting its contents when they fit or opening the standard inventory when they do not.
- `tp home` — teleports the character to their current spawn point.
- `set <name>` — saves the current player position as a named location. For example, `/set locationX` followed by `/tp locationX`.
- `set <name> <x> <z>` — saves supplied X/Z coordinates as a named location. For example, `/set locationX 1250 -800`.
- `delete <name>` — permanently removes a saved location from the world save.
- `tp <name>` — teleports to a saved location. `tp` by itself lists all saved locations and their coordinates.
- `freshcommands` — lists every available FreshCommands command.

Use commands directly in the console, such as `corpse tp` or `tp home`. In in-game chat, prefix the command with `/`, such as `/corpse tp` or `/tp home`.

## Notes

- Saved locations are shared by everyone on the same world and persist in that world's database save data.
- Commands work in single-player and for the hosting player.
- Dedicated-server and remote-client command routing is not included.
