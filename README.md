# NATO Quartermaster — SPT 4.1.x

NATO Quartermaster adds **Elias Ward**, a Western/NATO-focused trader built around complete usable kits instead of piles of random gun parts.

## Features

- Custom trader with lore and portrait support
- Western/NATO weapons and complete weapon offers
- Quality ammunition, armor, rigs, helmets, backpacks and headsets
- MREs and water
- All purchases use **roubles**
- No loyalty-level locks on normal stock
- Premium pricing and limited stock for balance
- Quartermaster primarily buys **PMC dogtags**
- Three short quests unlock premium armor, a high-end weapon/preset, helmet and restricted ammunition
- Configurable pricing, stock limits and item keyword pools

## Quest chain

### Inventory Check
Hand over 2 MREs and 2 bottles of water.

### Chain of Custody
Hand over 5 PMC dogtags.

### Restricted Issue
Hand over 10 PMC dogtags from level 15+ PMCs.

The exact restricted equipment is selected from the live vanilla trader database using the keyword pools in `config.json`.

## Balance

Quartermaster is meant to offer convenience, not free endgame gear. Normal stock carries a 25% convenience premium by default, stronger equipment has limited stock, and the best items are quest-gated.

## Custom portrait

Place a square JPEG at:

`assets/nato-quartermaster.jpg`

If the image is missing, the mod safely falls back to Peacekeeper's portrait.

## Build

Requires the .NET 10 SDK.

```powershell
dotnet restore
dotnet build -c Release
```

The release build creates:

`ReleaseZip\Michael-NATOQuartermaster-1.1.1.zip`

## Install

Extract the generated release ZIP into your SPT root. The compiled mod should end up at:

`SPT_Runtime\user\mods\Michael-NATOQuartermaster\`

## Known issue

In v1.1.1 the first quest can appear auto-accepted instead of waiting for the player to press Accept. Quest objectives and unlock rewards still function; this is planned for a follow-up fix.

## License

MIT
