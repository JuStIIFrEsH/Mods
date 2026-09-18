# FreshDedicatedStorage

Stackable single-item storage for Valheim with automatic inventory routing through Hugin's Reliquary.

**Author:** JuStIIFrEsH

## Links

- [Valheim Mods](https://justiifresh.com/valheim/)
- [Report a Bug](https://justiifresh.com/valheim/bug/)
- [Thunderstore](https://thunderstore.io/c/valheim/p/JuStIIFrEsH/FreshDedicatedStorage/)

## Requirements

- Valheim
- BepInExPack Valheim
- Jotunn

Thunderstore installs the required dependencies automatically.

## Installation

Install FreshDedicatedStorage through Thunderstore. The mod is currently intended for single-player worlds.

## Pieces

### Dedicated Storage Box

Build it from **Hammer > Furniture** for 5 Wood. It uses a compact wood chest model and has top, bottom, and side snap points for stacking.

- Press **E** on an empty box to open its one-slot assignment inventory. Put an item into that slot to set the box's item type and deposit that stack.
- Press **E** on an assigned box to deposit all matching, unequipped items from your inventory.
- Press **Shift+E** to withdraw one normal stack. If the box is empty, Shift+E clears its assignment.
- Press **Alt+E** to use one stored consumable without withdrawing it first.
- Hold **Shift** and scroll the mouse wheel while looking at a filled box to choose its withdrawal amount.
- A box stores one item type and an unlimited count. Items retain their item data when assigned.

### Hugin's Reliquary

Build it from **Hammer > Furniture** for 10 Wood. Press **E** to deposit matching, unequipped, non-hotbar inventory items into assigned Dedicated Storage Boxes within 50 meters.

## Notes

- Existing Dedicated Storage Boxes keep their assignments and quantities across updates.
- Filled boxes are protected from dismantling and ordinary building damage to prevent losing stored items. Empty them first.
- This release has not been tested for multiplayer.

## Credits

The embedded chest and column geometry are CC0 assets by Quaternius from Poly Pizza. Full attribution is in [`ASSET-CREDITS.md`](./ASSET-CREDITS.md).
