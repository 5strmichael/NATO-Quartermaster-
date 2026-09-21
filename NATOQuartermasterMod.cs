using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using SPTarkov.Common.Models.Logging;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Helpers.Server;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Enums;
using SPTarkov.Server.Core.Models.Spt.Config;
using SPTarkov.Server.Core.Models.Spt.Tables;
using SPTarkov.Server.Core.Routers;
using SPTarkov.Server.Core.Utils;
using SPTarkov.Server.Core.Utils.Cloners;
using SPTarkov.Server.Core.Utils.Json;

namespace NATOQuartermaster;

[Injectable(TypePriority = OnLoadOrder.TraderRegistration + 2)]
public sealed class NATOQuartermasterMod(
    ISptLogger<NATOQuartermasterMod> logger,
    ModHelper modHelper,
    TraderConfig traderConfig,
    RagfairConfig ragfairConfig,
    TimeUtil timeUtil,
    ICloner cloner,
    ImageRouter imageRouter,
    TradersTable tradersTable,
    TemplateTable templateTable,
    TraderRegistrationHelper traderRegistrationHelper) : IOnLoad
{
    private const string TraderId = "bb371fed0e82b224f2d82c0d";

    // Vanilla trader IDs.
    private const string PeacekeeperId = "5935c25fb3acc3127c3d8cd9";
    private const string RagmanId = "5ac3b934156ae10c4430e83c";
    private const string MechanicId = "5a7c2eca46aef81a7ca2145d";
    private const string TherapistId = "54cb57776803fa99248b456e";
    private const string JaegerId = "5c0647fdd443bc2504c2d371";

    private const string RoublesTpl = "5449016a4bdc2d6f028b456f";
    private const string DollarsTpl = "5696686a4bdc2da3298b456a";
    private const string EurosTpl = "569668774bdc2da2298b4568";

    private const string MreTpl = "590c5f0d86f77413997acfab";
    private const string WaterTpl = "5448fee04bdc2dbc018b4567";

    private const string QuestInventoryCheck = "71a001f1a11ce00100000001";
    private const string QuestChainOfCustody = "71a001f1a11ce00100000002";
    private const string QuestRestrictedIssue = "71a001f1a11ce00100000003";
    private const string QuestSupplyInterruption = "71a001f1a11ce00100000004";
    private const string QuestBlackLedger = "71a001f1a11ce00100000005";
    private const string QuestPriorityShipment = "71a001f1a11ce00100000006";

    private static readonly string[] DogtagTpls =
    [
        // USEC
        "59f32c3b86f77472a31742f0",
        "6662ea05f6259762c56f3189",
        "6662e9f37fa79a6d83730fa0",
        "6764207f2fa5e32733055c4a",
        "6764202ae307804338014c1a",
        "68418091b5b0c9e4c60f0e7a",
        // BEAR
        "59f32bb586f774757e1e8442",
        "6662e9cda7e0b43baa3d5f76",
        "6662e9aca7e0b43baa3d5f74",
        "684181208d035f60230f63f9",
        "684180bc51bf8645f7067bc8",
        "675dcb0545b1a2d108011b2b",
        "675dc9d37ae1a8792107ca96"
    ];

    public Task OnLoadAsync(CancellationToken cancellationToken)
    {
        var modPath = modHelper.GetAbsolutePathToModFolder(Assembly.GetExecutingAssembly());
        var traderBase = modHelper.GetJsonDataFromFile<TraderBase>(modPath, "db/base.json");
        var config = modHelper.GetJsonDataFromFile<NatoTraderConfig>(modPath, "config.json");

        var peacekeeper = tradersTable.GetTrader(PeacekeeperId);

        // V1.1 is ready for a custom portrait. If the JPG is absent, fall back safely to
        // Peacekeeper so an art change can never stop the server from loading the trader.
        var portraitPath = System.IO.Path.Combine(modPath, "assets", "nato-quartermaster.jpg");
        if (System.IO.File.Exists(portraitPath) && !string.IsNullOrWhiteSpace(traderBase.Avatar))
        {
            imageRouter.AddRoute(traderBase.Avatar.Replace(".jpg", "", StringComparison.OrdinalIgnoreCase), portraitPath);
        }
        else
        {
            traderBase.Avatar = peacekeeper.Base.Avatar;
            logger.Warning("[NATO Quartermaster] Custom portrait not found; using Peacekeeper portrait as a fallback.");
        }

        // Quests need a valid image route. Reuse Ward's registered portrait (or the Peacekeeper
        // fallback) instead of leaving the image empty and making the client spin forever.
        var questImage = traderBase.Avatar ?? peacekeeper.Base.Avatar ?? string.Empty;

        traderRegistrationHelper.SetTraderUpdateTime(
            traderConfig,
            traderBase,
            timeUtil.GetHoursAsSeconds(Math.Max(1, config.RefreshMinHours)),
            timeUtil.GetHoursAsSeconds(Math.Max(config.RefreshMinHours, config.RefreshMaxHours)));

        ragfairConfig.Traders.TryAdd(traderBase.Id, true);
        traderRegistrationHelper.AddTraderWithEmptyAssort(traderBase);
        traderRegistrationHelper.AddTraderLocales(
            traderBase,
            "Elias",
            "Former British military logistics NCO Elias Ward was attached to a Western procurement cell before the Tarkov cordon closed. When the evacuation fractured, Ward stayed behind with abandoned manifests, diverted shipments, and enough contacts to keep Western arms and field equipment moving. His prices are ugly, his paperwork immaculate, and he has little interest in buying your loot. Dogtags, however, are useful proof that a name can be removed from a manifest.");

        var targetTrader = tradersTable.GetTrader(traderBase.Id);
        var copiedSourceOffers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var peacekeeperCandidates = GetCandidates(PeacekeeperId, peacekeeper.Assort, templateTable, config);
        var mechanic = tradersTable.GetTrader(MechanicId);
        var mechanicCandidates = GetCandidates(MechanicId, mechanic.Assort, templateTable, config);
        var ragman = tradersTable.GetTrader(RagmanId);
        var ragmanCandidates = GetCandidates(RagmanId, ragman.Assort, templateTable, config);

        // Reserve the strongest offers for the six-quest progression before normal stock is built.
        // We choose several distinct premium items so later quests unlock genuinely better shelves.
        var restrictedVestCandidates = ragmanCandidates
            .Where(x => MatchesAny(x.InternalName, config.RestrictedVestKeywords))
            .GroupBy(x => x.Root.Template)
            .Select(x => x.OrderByDescending(y => y.PriceRoubles).First())
            .OrderByDescending(x => x.PriceRoubles)
            .Take(3)
            .ToList();

        var restrictedHelmetCandidates = ragmanCandidates
            .Where(x => MatchesAny(x.InternalName, config.RestrictedHelmetKeywords))
            .GroupBy(x => x.Root.Template)
            .Select(x => x.OrderByDescending(y => y.PriceRoubles).First())
            .OrderByDescending(x => x.PriceRoubles)
            .Take(3)
            .ToList();

        var allWeaponCandidates = peacekeeperCandidates
            .Concat(mechanicCandidates)
            .Where(x => MatchesAny(x.InternalName, config.WeaponKeywords))
            .ToList();

        var restrictedWeaponCandidates = allWeaponCandidates
            .Where(x => MatchesAny(x.InternalName, config.RestrictedWeaponKeywords))
            .GroupBy(x => GetPlatformKey(x.InternalName, config.RestrictedWeaponKeywords))
            .Select(group => group
                .OrderByDescending(x => x.SubtreeCount)
                .ThenByDescending(x => x.PriceRoubles)
                .First())
            .OrderByDescending(x => x.SubtreeCount)
            .ThenByDescending(x => x.PriceRoubles)
            .Take(3)
            .ToList();

        var restrictedAmmoCandidates = peacekeeperCandidates
            .Concat(mechanicCandidates)
            .Where(x => MatchesAny(x.InternalName, config.RestrictedAmmoKeywords))
            .GroupBy(x => x.Root.Template)
            .Select(x => x.OrderByDescending(y => y.PriceRoubles).First())
            .OrderByDescending(x => x.PriceRoubles)
            .Take(6)
            .ToList();

        var restrictedSourceKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var candidate in restrictedVestCandidates) restrictedSourceKeys.Add(candidate.SourceKey);
        foreach (var candidate in restrictedHelmetCandidates) restrictedSourceKeys.Add(candidate.SourceKey);
        foreach (var candidate in restrictedWeaponCandidates) restrictedSourceKeys.Add(candidate.SourceKey);
        foreach (var ammo in restrictedAmmoCandidates) restrictedSourceKeys.Add(ammo.SourceKey);

        var normalResults = new List<OfferResult>();

        // Complete Western weapon builds/presets. Prefer offers with more child parts, and cap
        // each platform so the trader stays curated instead of becoming Peacekeeper 2.0.
        var normalWeapons = allWeaponCandidates
            .Where(x => !restrictedSourceKeys.Contains(x.SourceKey))
            .GroupBy(x => GetPlatformKey(x.InternalName, config.WeaponKeywords))
            .SelectMany(group => group
                .OrderByDescending(x => x.SubtreeCount)
                .ThenByDescending(x => x.PriceRoubles)
                .Take(Math.Max(1, config.WeaponsPerPlatform)))
            .ToList();

        foreach (var candidate in normalWeapons)
        {
            TryCopyOffer(candidate, targetTrader.Assort, copiedSourceOffers, config.PriceMarkup,
                config.WeaponStock, config.WeaponBuyLimit, cloner, out var result);
            if (result is not null) normalResults.Add(result);
        }

        // Good, practical ammunition. Top-tier AP is reserved for quest 3.
        var normalAmmo = peacekeeperCandidates
            .Where(x => MatchesAny(x.InternalName, config.AmmoKeywords))
            .Where(x => !MatchesAny(x.InternalName, config.RestrictedAmmoKeywords))
            .GroupBy(x => x.Root.Template)
            .Select(x => x.OrderBy(y => y.PriceRoubles).First())
            .OrderBy(x => x.PriceRoubles)
            .ToList();

        foreach (var candidate in normalAmmo)
        {
            TryCopyOffer(candidate, targetTrader.Assort, copiedSourceOffers, config.PriceMarkup,
                config.AmmoStock, config.AmmoBuyLimit, cloner, out var result);
            if (result is not null) normalResults.Add(result);
        }

        // Useful magazines only; no rails, handguards, muzzle adapters, or random gunsmith clutter.
        var magazineCandidates = peacekeeperCandidates
            .Concat(mechanicCandidates)
            .Where(x => LooksLikeMagazine(x.InternalName))
            .Where(x => MatchesAny(x.InternalName, config.MagazineKeywords))
            .GroupBy(x => x.Root.Template)
            .Select(x => x.OrderBy(y => y.PriceRoubles).First())
            .OrderByDescending(x => x.PriceRoubles)
            .Take(Math.Max(0, config.MaxMagazineOffers))
            .ToList();

        foreach (var candidate in magazineCandidates)
        {
            TryCopyOffer(candidate, targetTrader.Assort, copiedSourceOffers, config.PriceMarkup,
                config.MagazineStock, config.MagazineBuyLimit, cloner, out var result);
            if (result is not null) normalResults.Add(result);
        }

        // Curated Western armor/rigs/helmets/packs/comms. The specific quest reward vest and
        // helmet are excluded until their quests are completed.
        var normalGear = ragmanCandidates
            .Where(x => MatchesAny(x.InternalName, config.GearKeywords))
            .Where(x => !restrictedSourceKeys.Contains(x.SourceKey))
            .GroupBy(x => x.Root.Template)
            .Select(x => x.OrderByDescending(y => y.PriceRoubles).First())
            .OrderByDescending(x => x.PriceRoubles)
            .Take(Math.Max(0, config.MaxGearOffers))
            .ToList();

        foreach (var candidate in normalGear)
        {
            TryCopyOffer(candidate, targetTrader.Assort, copiedSourceOffers, config.PriceMarkup,
                config.GearStock, config.GearBuyLimit, cloner, out var result);
            if (result is not null) normalResults.Add(result);
        }

        // Field supplies are part of the Quartermaster identity, but deliberately kept small.
        AddSupplyOffer(MreTpl, "MRE", targetTrader.Assort, copiedSourceOffers, config, templateTable, cloner,
            tradersTable.GetTrader(JaegerId).Assort, JaegerId,
            tradersTable.GetTrader(TherapistId).Assort, TherapistId);
        AddSupplyOffer(WaterTpl, "water", targetTrader.Assort, copiedSourceOffers, config, templateTable, cloner,
            tradersTable.GetTrader(TherapistId).Assort, TherapistId,
            tradersTable.GetTrader(JaegerId).Assort, JaegerId);

        // Restricted stock. Offers exist at LL1, but lower-case questassort mappings hide
        // them until the matching task is completed. SPT's lookup is case-sensitive.
        var quest1Unlocks = new List<OfferResult>();
        var quest2Unlocks = new List<OfferResult>();
        var quest3Unlocks = new List<OfferResult>();
        var quest4Unlocks = new List<OfferResult>();
        var quest5Unlocks = new List<OfferResult>();
        var quest6Unlocks = new List<OfferResult>();

        TryAddRestrictedOffer(restrictedVestCandidates.ElementAtOrDefault(0), quest1Unlocks,
            targetTrader.Assort, copiedSourceOffers, config, cloner, 1, 1);

        TryAddRestrictedOffer(restrictedWeaponCandidates.ElementAtOrDefault(0), quest2Unlocks,
            targetTrader.Assort, copiedSourceOffers, config, cloner, 1, 1);

        TryAddRestrictedOffer(restrictedHelmetCandidates.ElementAtOrDefault(0), quest3Unlocks,
            targetTrader.Assort, copiedSourceOffers, config, cloner, 1, 1);
        foreach (var candidate in restrictedAmmoCandidates.Take(2))
        {
            TryAddRestrictedOffer(candidate, quest3Unlocks, targetTrader.Assort, copiedSourceOffers,
                config, cloner, config.RestrictedAmmoStock, config.RestrictedAmmoBuyLimit);
        }

        // Quest 4 opens a second high-end armor shelf.
        TryAddRestrictedOffer(restrictedVestCandidates.ElementAtOrDefault(1), quest4Unlocks,
            targetTrader.Assort, copiedSourceOffers, config, cloner, 1, 1);
        TryAddRestrictedOffer(restrictedHelmetCandidates.ElementAtOrDefault(1), quest4Unlocks,
            targetTrader.Assort, copiedSourceOffers, config, cloner, 1, 1);

        // Quest 5 opens another complete premium rifle plus more restricted ammunition.
        TryAddRestrictedOffer(restrictedWeaponCandidates.ElementAtOrDefault(1), quest5Unlocks,
            targetTrader.Assort, copiedSourceOffers, config, cloner, 1, 1);
        foreach (var candidate in restrictedAmmoCandidates.Skip(2).Take(2))
        {
            TryAddRestrictedOffer(candidate, quest5Unlocks, targetTrader.Assort, copiedSourceOffers,
                config, cloner, config.RestrictedAmmoStock, config.RestrictedAmmoBuyLimit);
        }

        // Final quest opens the black rack: another elite rifle, armor, helmet, and remaining AP.
        TryAddRestrictedOffer(restrictedWeaponCandidates.ElementAtOrDefault(2), quest6Unlocks,
            targetTrader.Assort, copiedSourceOffers, config, cloner, 1, 1);
        TryAddRestrictedOffer(restrictedVestCandidates.ElementAtOrDefault(2), quest6Unlocks,
            targetTrader.Assort, copiedSourceOffers, config, cloner, 1, 1);
        TryAddRestrictedOffer(restrictedHelmetCandidates.ElementAtOrDefault(2), quest6Unlocks,
            targetTrader.Assort, copiedSourceOffers, config, cloner, 1, 1);
        foreach (var candidate in restrictedAmmoCandidates.Skip(4))
        {
            TryAddRestrictedOffer(candidate, quest6Unlocks, targetTrader.Assort, copiedSourceOffers,
                config, cloner, config.RestrictedAmmoStock, config.RestrictedAmmoBuyLimit);
        }

        RegisterQuestChain(
            targetTrader,
            templateTable,
            traderRegistrationHelper,
            questImage,
            quest1Unlocks,
            quest2Unlocks,
            quest3Unlocks,
            quest4Unlocks,
            quest5Unlocks,
            quest6Unlocks,
            cloner);

        va