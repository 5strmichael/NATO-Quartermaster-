# NATO Quartermaster — SPT 4.1.x

V1.1 turns the first proof-of-concept trader into a small gameplay package instead of a Peacekeeper/Ragman mirror.

## What's new in 1.1

- Elias Ward / **Quartermaster** now has lore and custom trader text.
- All offers are sold for **roubles**.
- Normal stock has **no loyalty-level locks**.
- Normal stock is intentionally expensive (25% convenience premium by default).
- Restricted quest stock carries a larger premium.
- Quartermaster's buy whitelist is **PMC dogtags only**.
- Inventory is curated toward complete Western/NATO-style weapons, magazines, ammunition, armor/rigs/helmets/packs/headsets, MREs, and water.
- Full weapon offers are preferred over loose weapon parts.
- Three small quests unlock a premium vest, a high-end complete weapon/preset, a premium helmet, and restricted ammunition.
- Custom portrait support is wired in at `assets/nato-quartermaster.jpg`.

## Quest chain

### Inventory Check
Hand over 2 MREs and 2 bottles of water.

Unlocks the restricted premium vest offer.

### Chain of Custody
Hand over 5 PMC dogtags.

Unlocks the restricted high-end complete weapon/preset.

### Restricted Issue
Hand over 10 PMC dogtags from level 15+ PMCs.

Unlocks the restricted helmet and up to three high-end ammunition offers.

The exact restricted weapon/gear is selected from the live vanilla trader database using the keyword pools in `config.json`. This keeps the mod from depending on one fragile set of offer IDs.

## Balance philosophy

Quartermaster is intended to save time, not trivialize progression:

- No loyalty-level grind for normal stock.
- Good complete kits are available early.
- Prices are intentionally higher than the source trader/flea baseline.
- Weapons and premium armor have low stock and purchase limits.
- The strongest equipment remains quest locked.
- He will not act as a high-value resale dump; his buy whitelist is dogtags only.

## Custom portrait

The server-side image route is already implemented.

Place a square JPEG here before building:

`assets/nato-quartermaster.jpg`

If the image is missing, the mod uses Peacekeeper's portrait so testing can continue safely.

## Config

`config.json` exposes:

- Normal and restricted price markups
- USD/EUR-to-RUB conversion used when copying vanilla offers
- Restock timing
- Stock and purchase limits
- Weapon platform keywords
- Normal and restricted ammunition keywords
- Magazine families
- Western gear families
- Premium vest and helmet keyword pools

## Build

Install the .NET 10 SDK, open a terminal in the source folder, then run:

```powershell
dotnet restore
dotnet build -c Release
```

The release build creates:

`ReleaseZip\Michael-NATOQuartermaster-1.1.0.zip`

## Install

Extract the generated release ZIP into the SPT root. The compiled mod lands at:

`SPT_Runtime\user\mods\Michael-NATOQuartermaster\`

Back up your profile before testing a new quest/trader build. For the first V1.1 test, a fresh test profile is safest because quests and trader assort unlocks are now involved.
