# NATO Quartermaster — SPT 4.1.x

NATO Quartermaster adds **Elias Ward**, a Western/NATO-focused trader built around complete usable kits, premium pricing, limited stock, and quest-gated restricted inventory.

## v1.1.3

This update expands the progression to six quests and hardens the quest/trader integration.

### Fixes

- Fixed quest images endlessly loading by assigning a valid registered image route.
- Corrected quest-assort status keys to the lowercase values expected by SPT (`started`, `success`, `fail`).
- Added extra startup logging for quest unlock counts to make assort problems easier to diagnose.
- Synchronized project/release versioning to **1.1.3**.

> The reported post-raid assort issue should be retested with this build. The incorrect quest-assort key casing was corrected and is a strong candidate for the restricted-stock problem, but this release should be validated in-game before calling it fully resolved.

## Quest chain

1. **Inventory Check** — hand over 2 MREs and 2 bottles of water. Unlocks premium armor.
2. **Chain of Custody** — hand over 5 PMC dogtags. Unlocks a high-end complete Western rifle.
3. **Restricted Issue** — hand over 10 PMC dogtags from level 15+ PMCs. Unlocks premium head protection and restricted ammunition.
4. **Supply Interruption** — eliminate 12 Scavs on Customs. Unlocks another high-end armor/helmet shelf.
5. **Black Ledger** — hand over 8 PMC dogtags from level 25+ PMCs. Unlocks another premium rifle plus more AP ammunition.
6. **Priority Shipment** — eliminate 8 PMCs and hand over 12 PMC dogtags from level 30+ PMCs. Unlocks the **black rack**: another elite rifle, premium armor/helmet, and remaining top-end ammunition available from Ward.

The exact restricted equipment is selected from the live vanilla trader database using the keyword pools in `config.json`, so the mod stays compatible with the current SPT item database instead of hardcoding one specific build.

## Features

- Custom trader with lore and portrait
- Western/NATO weapons and complete weapon offers
- Quality ammunition, armor, rigs, helmets, backpacks, and headsets
- Roubles only
- No loyalty-level locks on normal stock
- Premium pricing and limited stock
- Quartermaster primarily buys PMC dogtags
- Six-quest progression with high-end purchase unlocks
- Configurable pricing, stock limits, and keyword pools

## Build

Requires the .NET 10 SDK.

```powershell
dotnet restore
dotnet build -c Release
```

The release build creates:

`ReleaseZip\Michael-NATOQuartermaster-1.1.3.zip`

## Install

Extract the generated release ZIP into your SPT root. The compiled mod should end up at:

`SPT_Runtime\user\mods\Michael-NATOQuartermaster\`

## Testing notes

For v1.1.3, specifically test:

- all six quests appear in sequence rather than at once;
- quest images load;
- restricted items remain hidden until their quest is completed;
- complete/extract from a raid, then reopen Quartermaster and verify his assort still loads;
- complete at least one quest that unlocks stock, restart the game/server, and verify the unlock persists.

## License

MIT
