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

        var rootOffers = targetTrader.Assort.Items.Count(x => string.Equals(x.ParentId, "hideout", StringComparison.OrdinalIgnoreCase));
        logger.Success($"[NATO Quartermaster] V1.1.3 loaded: {rootOffers} curated offers and 6 quests.");
        logger.Success($"[NATO Quartermaster] Quest unlocks: Q1={quest1Unlocks.Count}, Q2={quest2Unlocks.Count}, Q3={quest3Unlocks.Count}, Q4={quest4Unlocks.Count}, Q5={quest5Unlocks.Count}, Q6={quest6Unlocks.Count}.");

        if (quest1Unlocks.Count == 0) logger.Warning("[NATO Quartermaster] Quest 1 has no matching premium vest unlock.");
        if (quest2Unlocks.Count == 0) logger.Warning("[NATO Quartermaster] Quest 2 has no matching premium weapon unlock.");
        if (quest3Unlocks.Count == 0) logger.Warning("[NATO Quartermaster] Quest 3 has no matching restricted gear/ammo unlocks.");
        if (quest4Unlocks.Count == 0) logger.Warning("[NATO Quartermaster] Quest 4 has no matching second-tier armor unlocks.");
        if (quest5Unlocks.Count == 0) logger.Warning("[NATO Quartermaster] Quest 5 has no matching second premium weapon/ammo unlocks.");
        if (quest6Unlocks.Count == 0) logger.Warning("[NATO Quartermaster] Quest 6 has no matching black-rack unlocks.");

        return Task.CompletedTask;
    }

    private static List<OfferCandidate> GetCandidates(
        string sourceTraderId,
        TraderAssort source,
        TemplateTable templateTable,
        NatoTraderConfig config)
    {
        var result = new List<OfferCandidate>();
        foreach (var root in source.Items.Where(x => string.Equals(x.ParentId, "hideout", StringComparison.OrdinalIgnoreCase)))
        {
            var template = templateTable.Items.GetValueOrDefault(root.Template);
            if (template is null || string.IsNullOrWhiteSpace(template.Name))
            {
                continue;
            }

            var subtree = CollectSubtree(source.Items, root.Id.ToString());
            var price = GetRoublePrice(source, root.Id, subtree, templateTable, config);
            if (price <= 0)
            {
                continue;
            }

            result.Add(new OfferCandidate(
                sourceTraderId,
                source,
                root,
                template.Name.ToLowerInvariant(),
                price,
                subtree.Count));
        }

        return result;
    }

    private static double GetRoublePrice(
        TraderAssort source,
        MongoId rootId,
        IReadOnlyCollection<Item> subtree,
        TemplateTable templateTable,
        NatoTraderConfig config)
    {
        if (source.BarterScheme.TryGetValue(rootId, out var alternatives))
        {
            foreach (var alternative in alternatives)
            {
                if (alternative.Count != 1)
                {
                    continue;
                }

                var requirement = alternative[0];
                var count = requirement.Count ?? 0;
                if (count <= 0)
                {
                    continue;
                }

                var tpl = requirement.Template.ToString();
                if (tpl == RoublesTpl) return count;
                if (tpl == DollarsTpl) return count * config.UsdToRub;
                if (tpl == EurosTpl) return count * config.EurToRub;
            }
        }

        // Some valuable vanilla equipment is barter-only. Use the live flea-price table as a
        // fallback baseline. For a built weapon/armour preset, price the entire subtree so the
        // attachments/plates are not accidentally given away for the root item's value.
        return subtree.Sum(item => templateTable.Prices.GetValueOrDefault(item.Template, 0));
    }

    private static bool TryCopyOffer(
        OfferCandidate candidate,
        TraderAssort destination,
        HashSet<string> copiedSourceOffers,
        double markup,
        int stock,
        int buyLimit,
        ICloner cloner,
        out OfferResult? result)
    {
        result = null;
        if (!copiedSourceOffers.Add(candidate.SourceKey))
        {
            return false;
        }

        var sourceSubtree = CollectSubtree(candidate.SourceAssort.Items, candidate.Root.Id.ToString());
        if (sourceSubtree.Count == 0)
        {
            return false;
        }

        var idMap = sourceSubtree.ToDictionary(
            x => x.Id.ToString(),
            x => StableId($"{TraderId}:{candidate.SourceTraderId}:{candidate.Root.Id}:{x.Id}"),
            StringComparer.OrdinalIgnoreCase);

        var copiedItems = cloner.Clone(sourceSubtree);
        foreach (var item in copiedItems)
        {
            var oldId = item.Id.ToString();
            item.Id = idMap[oldId];

            if (item.ParentId is not null && idMap.TryGetValue(item.ParentId, out var newParent))
            {
                item.ParentId = newParent;
            }
        }

        var newRootId = idMap[candidate.Root.Id.ToString()];
        var root = copiedItems.First(x => x.Id.ToString() == newRootId);
        root.ParentId = "hideout";
        root.SlotId = "hideout";
        root.Upd ??= new Upd();
        root.Upd.UnlimitedCount = false;
        root.Upd.StackObjectsCount = Math.Max(1, stock);
        root.Upd.BuyRestrictionMax = Math.Max(1, buyLimit);
        root.Upd.BuyRestrictionCurrent = 0;

        destination.Items.AddRange(copiedItems);

        var finalPrice = RoundRoubles(candidate.PriceRoubles * Math.Max(1.0, markup));
        destination.BarterScheme[newRootId] =
        [
            [
                new BarterScheme
                {
                    Count = finalPrice,
                    Template = RoublesTpl
                }
            ]
        ];
        destination.LoyalLevelItems[newRootId] = 1;

        result = new OfferResult(newRootId, copiedItems, candidate.InternalName, finalPrice);
        return true;
    }

    private static void TryAddRestrictedOffer(
        OfferCandidate? candidate,
        ICollection<OfferResult> unlocks,
        TraderAssort destination,
        HashSet<string> copiedSourceOffers,
        NatoTraderConfig config,
        ICloner cloner,
        int stock,
        int buyLimit)
    {
        if (candidate is null)
        {
            return;
        }

        TryCopyOffer(candidate, destination, copiedSourceOffers, config.RestrictedPriceMarkup,
            stock, buyLimit, cloner, out var result);
        if (result is not null)
        {
            unlocks.Add(result);
        }
    }

    private static void AddSupplyOffer(
        string templateId,
        string label,
        TraderAssort destination,
        HashSet<string> copiedSourceOffers,
        NatoTraderConfig config,
        TemplateTable templateTable,
        ICloner cloner,
        TraderAssort primarySource,
        string primarySourceId,
        TraderAssort secondarySource,
        string secondarySourceId)
    {
        foreach (var (source, sourceId) in new[]
                 {
                     (primarySource, primarySourceId),
                     (secondarySource, secondarySourceId)
                 })
        {
            var candidate = GetCandidates(sourceId, source, templateTable, config)
                .FirstOrDefault(x => x.Root.Template.ToString() == templateId);
            if (candidate is null)
            {
                continue;
            }

            TryCopyOffer(candidate, destination, copiedSourceOffers, config.PriceMarkup,
                config.SupplyStock, config.SupplyBuyLimit, cloner, out _);
            return;
        }
    }

    private static void RegisterQuestChain(
        Trader targetTrader,
        TemplateTable templateTable,
        TraderRegistrationHelper traderRegistrationHelper,
        string questImage,
        IReadOnlyCollection<OfferResult> quest1Unlocks,
        IReadOnlyCollection<OfferResult> quest2Unlocks,
        IReadOnlyCollection<OfferResult> quest3Unlocks,
        IReadOnlyCollection<OfferResult> quest4Unlocks,
        IReadOnlyCollection<OfferResult> quest5Unlocks,
        IReadOnlyCollection<OfferResult> quest6Unlocks,
        ICloner cloner)
    {
        AddQuestUnlockMappings(targetTrader, QuestInventoryCheck, quest1Unlocks);
        AddQuestUnlockMappings(targetTrader, QuestChainOfCustody, quest2Unlocks);
        AddQuestUnlockMappings(targetTrader, QuestRestrictedIssue, quest3Unlocks);
        AddQuestUnlockMappings(targetTrader, QuestSupplyInterruption, quest4Unlocks);
        AddQuestUnlockMappings(targetTrader, QuestBlackLedger, quest5Unlocks);
        AddQuestUnlockMappings(targetTrader, QuestPriorityShipment, quest6Unlocks);

        var q1MreCondition = CreateHandoverCondition(
            StableId($"{QuestInventoryCheck}:mre"), [MreTpl], 2, 0, 0);
        var q1WaterCondition = CreateHandoverCondition(
            StableId($"{QuestInventoryCheck}:water"), [WaterTpl], 2, 0, 1);
        var q2DogtagCondition = CreateHandoverCondition(
            StableId($"{QuestChainOfCustody}:dogtags"), DogtagTpls, 5, 0, 0);
        var q3DogtagCondition = CreateHandoverCondition(
            StableId($"{QuestRestrictedIssue}:dogtags"), DogtagTpls, 10, 15, 0);
        var q4ScavCondition = CreateKillCondition(
            QuestSupplyInterruption, "Savage", 12, 0, "bigmap");
        var q5DogtagCondition = CreateHandoverCondition(
            StableId($"{QuestBlackLedger}:dogtags"), DogtagTpls, 8, 25, 0);
        var q6PmcCondition = CreateKillCondition(
            QuestPriorityShipment, "AnyPmc", 8, 0, null);
        var q6DogtagCondition = CreateHandoverCondition(
            StableId($"{QuestPriorityShipment}:dogtags"), DogtagTpls, 12, 30, 1);

        var quest1 = CreateQuest(
            QuestInventoryCheck,
            "Inventory Check",
            [CreateLevelCondition(StableId($"{QuestInventoryCheck}:level"), 1)],
            [q1MreCondition, q1WaterCondition],
            1000,
            questImage,
            quest1Unlocks,
            cloner);

        var quest2 = CreateQuest(
            QuestChainOfCustody,
            "Chain of Custody",
            [CreateQuestRequirement(StableId($"{QuestChainOfCustody}:previous"), QuestInventoryCheck)],
            [q2DogtagCondition],
            2500,
            questImage,
            quest2Unlocks,
            cloner);

        var quest3 = CreateQuest(
            QuestRestrictedIssue,
            "Restricted Issue",
            [CreateQuestRequirement(StableId($"{QuestRestrictedIssue}:previous"), QuestChainOfCustody)],
            [q3DogtagCondition],
            5000,
            questImage,
            quest3Unlocks,
            cloner);

        var quest4 = CreateQuest(
            QuestSupplyInterruption,
            "Supply Interruption",
            [CreateQuestRequirement(StableId($"{QuestSupplyInterruption}:previous"), QuestRestrictedIssue)],
            [q4ScavCondition],
            7500,
            questImage,
            quest4Unlocks,
            cloner);

        var quest5 = CreateQuest(
            QuestBlackLedger,
            "Black Ledger",
            [CreateQuestRequirement(StableId($"{QuestBlackLedger}:previous"), QuestSupplyInterruption)],
            [q5DogtagCondition],
            10000,
            questImage,
            quest5Unlocks,
            cloner);

        var quest6 = CreateQuest(
            QuestPriorityShipment,
            "Priority Shipment",
            [CreateQuestRequirement(StableId($"{QuestPriorityShipment}:previous"), QuestBlackLedger)],
            [q6PmcCondition, q6DogtagCondition],
            15000,
            questImage,
            quest6Unlocks,
            cloner);

        templateTable.Quests[quest1.Id] = quest1;
        templateTable.Quests[quest2.Id] = quest2;
        templateTable.Quests[quest3.Id] = quest3;
        templateTable.Quests[quest4.Id] = quest4;
        templateTable.Quests[quest5.Id] = quest5;
        templateTable.Quests[quest6.Id] = quest6;

        traderRegistrationHelper.AddLocaleEntries(BuildQuestLocaleEntries(
            q1MreCondition.Id.ToString(),
            q1WaterCondition.Id.ToString(),
            q2DogtagCondition.Id.ToString(),
            q3DogtagCondition.Id.ToString(),
            q4ScavCondition.Id.ToString(),
            q5DogtagCondition.Id.ToString(),
            q6PmcCondition.Id.ToString(),
            q6DogtagCondition.Id.ToString()));
    }

    private static void AddQuestUnlockMappings(
        Trader targetTrader,
        string questId,
        IEnumerable<OfferResult> unlocks)
    {
        foreach (var unlock in unlocks)
        {
            targetTrader.QuestAssort["success"][unlock.RootId] = questId;
        }
    }

    private static Quest CreateQuest(
        string questId,
        string questName,
        List<QuestCondition> startConditions,
        List<QuestCondition> finishConditions,
        int experience,
        string questImage,
        IReadOnlyCollection<OfferResult> unlocks,
        ICloner cloner)
    {
        var successRewards = new List<Reward>
        {
            new()
            {
                Id = StableId($"{questId}:xp"),
                Type = RewardType.Experience,
                Value = experience,
                Index = 0,
                Unknown = false,
                AvailableInGameEditions = []
            }
        };

        var rewardIndex = 1;
        foreach (var unlock in unlocks)
        {
            var rewardItems = cloner.Clone(unlock.Items);
            var rewardIdMap = rewardItems.ToDictionary(
                item => item.Id.ToString(),
                item => StableId($"{questId}:reward:{unlock.RootId}:{item.Id}"),
                StringComparer.OrdinalIgnoreCase);

            foreach (var rewardItem in rewardItems)
            {
                var oldId = rewardItem.Id.ToString();
                rewardItem.Id = rewardIdMap[oldId];
                if (rewardItem.ParentId is not null && rewardIdMap.TryGetValue(rewardItem.ParentId, out var newParent))
                {
                    rewardItem.ParentId = newParent;
                }
            }

            var rewardRootId = rewardIdMap[unlock.RootId.ToString()];
            var rewardRoot = rewardItems.First(x => x.Id.ToString() == rewardRootId);
            rewardRoot.ParentId = null;
            rewardRoot.SlotId = null;

            successRewards.Add(new Reward
            {
                Id = StableId($"{questId}:unlock:{unlock.RootId}"),
                Type = RewardType.AssortmentUnlock,
                Index = rewardIndex++,
                Target = rewardRootId,
                Items = rewardItems,
                LoyaltyLevel = 1,
                TraderId = new StringOrInt(TraderId, null),
                Unknown = false,
                AvailableInGameEditions = []
            });
        }

        return new Quest
        {
            QuestName = questName,
            Id = questId,
            CanShowNotificationsInGame = true,
            Conditions = new QuestConditionTypes
            {
                Started = [],
                AvailableForFinish = finishConditions,
                AvailableForStart = startConditions,
                Success = [],
                Fail = []
            },
            Description = $"{questId} description",
            FailMessageText = $"{questId} failMessageText",
            Name = $"{questId} name",
            Note = $"{questId} note",
            TraderId = TraderId,
            Location = "any",
            Image = questImage,
            Type = QuestTypeEnum.Completion,
            IsKey = false,
            Restartable = false,
            InstantComplete = false,
            SecretQuest = false,
            StartedMessageText = $"{questId} startedMessageText",
            SuccessMessageText = $"{questId} successMessageText",
            AcceptPlayerMessage = $"{questId} acceptPlayerMessage",
            AcceptanceAndFinishingSource = "eft",
            DeclinePlayerMessage = $"{questId} declinePlayerMessage",
            CompletePlayerMessage = $"{questId} completePlayerMessage",
            Rewards = new Dictionary<string, List<Reward>>
            {
                ["Started"] = [],
                ["Success"] = successRewards,
                ["Fail"] = []
            },
            Status = 0,
            KeyQuest = false,
            ChangeQuestMessageText = $"{questId} changeQuestMessageText",
            Side = "Pmc",
            ProgressSource = "eft",
            RankingModes = [],
            GameModes = [],
            ArenaLocations = []
        };
    }

    private static QuestCondition CreateLevelCondition(string id, int level)
    {
        return new QuestCondition
        {
            Id = id,
            Index = 0,
            CompareMethod = ">=",
            DynamicLocale = false,
            GlobalQuestCounterId = string.Empty,
            VisibilityConditions = [],
            ParentId = string.Empty,
            Value = level,
            ConditionType = "Level"
        };
    }

    private static QuestCondition CreateQuestRequirement(string id, string previousQuestId)
    {
        return new QuestCondition
        {
            Id = id,
            Index = 0,
            DynamicLocale = false,
            GlobalQuestCounterId = string.Empty,
            VisibilityConditions = [],
            ParentId = string.Empty,
            Target = new ListOrT<string>(null, previousQuestId),
            Status = [QuestStatusEnum.Success],
            AvailableAfter = 0,
            Dispersion = 0,
            ConditionType = "Quest"
        };
    }

    private static QuestCondition CreateKillCondition(
        string questId,
        string target,
        int count,
        int index,
        string? locationId)
    {
        var counterConditions = new List<QuestConditionCounterCondition>
        {
            new()
            {
                Id = new MongoId(StableId($"{questId}:kill-target")),
                CompareMethod = ">=",
                ConditionType = "Kills",
                ResetOnSessionEnd = false,
                Target = new ListOrT<string>(null, target),
                Value = 1,
                BodyPart = [],
                Daytime = new DaytimeCounter { From = 0, To = 0 },
                Distance = new CounterConditionDistance { CompareMethod = ">=", Value = 0 },
                DynamicLocale = false,
                EnemyEquipmentExclusive = [],
                EnemyEquipmentInclusive = [],
                EnemyHealthEffects = [],
                SavageRole = [],
                Weapon = [],
                WeaponCaliber = [],
                WeaponModsExclusive = [],
                WeaponModsInclusive = []
            }
        };

        if (!string.IsNullOrWhiteSpace(locationId))
        {
            counterConditions.Add(new QuestConditionCounterCondition
            {
                Id = new MongoId(StableId($"{questId}:kill-location")),
                ConditionType = "Location",
                DynamicLocale = false,
                Target = new ListOrT<string>([locationId], null)
            });
        }

        return new QuestCondition
        {
            CompleteInSeconds = 0,
            ConditionType = "CounterCreator",
            Counter = new QuestConditionCounter
            {
                Id = StableId($"{questId}:kill-counter"),
                Conditions = counterConditions
            },
            DoNotResetIfCounterCompleted = false,
            DynamicLocale = false,
            GlobalQuestCounterId = string.Empty,
            Id = new MongoId(StableId($"{questId}:kill")),
            Index = index,
            IsNecessary = true,
            IsResetOnConditionFailed = false,
            OneSessionOnly = false,
            ParentId = string.Empty,
            Type = "Elimination",
            Value = count,
            VisibilityConditions = []
        };
    }

    private static QuestCondition CreateHandoverCondition(
        string id,
        IEnumerable<string> targetTemplates,
        int count,
        int dogtagLevel,
        int index)
    {
        return new QuestCondition
        {
            Id = id,
            Index = index,
            DynamicLocale = false,
            GlobalQuestCounterId = string.Empty,
            VisibilityConditions = [],
            ParentId = string.Empty,
            Target = new ListOrT<string>(targetTemplates.ToList(), null),
            Value = count,
            OnlyFoundInRaid = false,
            DogtagLevel = dogtagLevel,
            MaxDurability = 100,
            MinDurability = 0,
            IsEncoded = false,
            ConditionType = "HandoverItem"
        };
    }

    private static Dictionary<string, string> BuildQuestLocaleEntries(
        string q1MreCondition,
        string q1WaterCondition,
        string q2DogtagCondition,
        string q3DogtagCondition,
        string q4ScavCondition,
        string q5DogtagCondition,
        string q6PmcCondition,
        string q6DogtagCondition)
    {
        return new Dictionary<string, string>
        {
            [$"{QuestInventoryCheck} name"] = "Inventory Check",
            [$"{QuestInventoryCheck} description"] = "A supply chain is only as reliable as the person standing at the end of it. Bring me two MREs and two bottles of water. Nothing glamorous. If you can handle the boring work without losing half of it, I may open the better racks to you.",
            [$"{QuestInventoryCheck} note"] = "Ward wants basic field rations before discussing restricted stock.",
            [$"{QuestInventoryCheck} startedMessageText"] = "Two meals. Two waters. Try not to turn procurement into an adventure.",
            [$"{QuestInventoryCheck} successMessageText"] = "Good. Inventory accounted for. I've opened one of the restricted armor racks.",
            [$"{QuestInventoryCheck} failMessageText"] = "Impressive. You managed to lose a logistics task.",
            [$"{QuestInventoryCheck} acceptPlayerMessage"] = "I'll get it handled.",
            [$"{QuestInventoryCheck} declinePlayerMessage"] = "Not interested.",
            [$"{QuestInventoryCheck} completePlayerMessage"] = "Inventory accounted for.",
            [$"{QuestInventoryCheck} changeQuestMessageText"] = "The manifest changed. The requirement did not.",
            [q1MreCondition] = "Hand over 2 MRE ration packs",
            [q1WaterCondition] = "Hand over 2 bottles of water",

            [$"{QuestChainOfCustody} name"] = "Chain of Custody",
            [$"{QuestChainOfCustody} description"] = "One of my manifests has too many names still marked active. Bring me five PMC dogtags. I don't care which patch they wore. I need proof before I move restricted weapons off the books.",
            [$"{QuestChainOfCustody} note"] = "Dogtags are evidence, not trophies, at least according to Ward.",
            [$"{QuestChainOfCustody} startedMessageText"] = "Five tags. Legible. I have enough mysteries in the inventory system already.",
            [$"{QuestChainOfCustody} successMessageText"] = "Good. Five names removed from the manifest. One restricted rifle is now on your purchase list.",
            [$"{QuestChainOfCustody} failMessageText"] = "Chain of custody broken. Again.",
            [$"{QuestChainOfCustody} acceptPlayerMessage"] = "I'll bring proof.",
            [$"{QuestChainOfCustody} declinePlayerMessage"] = "Keep the rifle.",
            [$"{QuestChainOfCustody} completePlayerMessage"] = "The tags are yours.",
            [$"{QuestChainOfCustody} changeQuestMessageText"] = "Same manifest. More dead ink.",
            [q2DogtagCondition] = "Hand over 5 PMC dogtags",

            [$"{QuestRestrictedIssue} name"] = "Restricted Issue",
            [$"{QuestRestrictedIssue} description"] = "The first controlled cabinet is armor and ammunition that attracts questions. Bring me ten dogtags from operators level fifteen or higher. If you're working at that level, I can justify moving serious inventory to your account.",
            [$"{QuestRestrictedIssue} note"] = "Ward's first restricted cabinet is reserved for proven operators.",
            [$"{QuestRestrictedIssue} startedMessageText"] = "Ten experienced operators. Level fifteen minimum.",
            [$"{QuestRestrictedIssue} successMessageText"] = "Enough. The restricted cabinet is open. Better protection and premium ammunition are now on the books.",
            [$"{QuestRestrictedIssue} failMessageText"] = "Restricted means restricted. Come back when the paperwork is convincing.",
            [$"{QuestRestrictedIssue} acceptPlayerMessage"] = "Open the cabinet when I get back.",
            [$"{QuestRestrictedIssue} declinePlayerMessage"] = "Not worth it.",
            [$"{QuestRestrictedIssue} completePlayerMessage"] = "Ten qualified tags.",
            [$"{QuestRestrictedIssue} changeQuestMessageText"] = "The cabinet remains locked.",
            [q3DogtagCondition] = "Hand over 10 PMC dogtags from level 15+ operators",

            [$"{QuestSupplyInterruption} name"] = "Supply Interruption",
            [$"{QuestSupplyInterruption} description"] = "A scav crew has been hitting the Customs route I use to move protective equipment inland. Twelve of them should be enough to make the next convoy boring again. I like boring convoys.",
            [$"{QuestSupplyInterruption} note"] = "Clear Ward's Customs supply route and a second premium armor shelf becomes available.",
            [$"{QuestSupplyInterruption} startedMessageText"] = "Customs. Twelve scavengers. Keep my route open.",
            [$"{QuestSupplyInterruption} successMessageText"] = "Route is moving again. I've added another set of premium armor and head protection to your account.",
            [$"{QuestSupplyInterruption} failMessageText"] = "The route is still blocked.",
            [$"{QuestSupplyInterruption} acceptPlayerMessage"] = "I'll clear the route.",
            [$"{QuestSupplyInterruption} declinePlayerMessage"] = "Find another route.",
            [$"{QuestSupplyInterruption} completePlayerMessage"] = "Customs is clear enough.",
            [$"{QuestSupplyInterruption} changeQuestMessageText"] = "Convoy schedule remains unchanged.",
            [q4ScavCondition] = "Eliminate 12 Scavs on Customs",

            [$"{QuestBlackLedger} name"] = "Black Ledger",
            [$"{QuestBlackLedger} description"] = "The public manifest is clean. The other ledger isn't. I need eight tags from operators level twenty-five or higher before I move the next rifle and ammunition allotment out of reserve.",
            [$"{QuestBlackLedger} note"] = "Experienced PMC tags buy access to Ward's deeper reserve inventory.",
            [$"{QuestBlackLedger} startedMessageText"] = "Eight tags. Level twenty-five or better. No tourists.",
            [$"{QuestBlackLedger} successMessageText"] = "The ledger balances. Another premium rifle and another AP allocation are now available to you.",
            [$"{QuestBlackLedger} failMessageText"] = "The numbers do not balance.",
            [$"{QuestBlackLedger} acceptPlayerMessage"] = "I'll balance it.",
            [$"{QuestBlackLedger} declinePlayerMessage"] = "Keep it off the books.",
            [$"{QuestBlackLedger} completePlayerMessage"] = "Eight experienced names accounted for.",
            [$"{QuestBlackLedger} changeQuestMessageText"] = "The ledger is still open.",
            [q5DogtagCondition] = "Hand over 8 PMC dogtags from level 25+ operators",

            [$"{QuestPriorityShipment} name"] = "Priority Shipment",
            [$"{QuestPriorityShipment} description"] = "Last allocation. The stock in this shipment was never meant for open sale. Remove eight PMCs from the board and bring me twelve tags from level thirty or higher. Do that, and I'll stop pretending the black rack doesn't exist.",
            [$"{QuestPriorityShipment} note"] = "Ward's final contract opens his best available Western equipment.",
            [$"{QuestPriorityShipment} startedMessageText"] = "Eight PMCs down. Twelve level-thirty tags on my desk. Then we discuss the black rack.",
            [$"{QuestPriorityShipment} successMessageText"] = "Contract closed. The black rack is yours to buy from: elite rifle, armor, helmet, and the remaining premium ammunition I can source.",
            [$"{QuestPriorityShipment} failMessageText"] = "Priority inventory stays sealed until the contract is complete.",
            [$"{QuestPriorityShipment} acceptPlayerMessage"] = "Get the shipment ready.",
            [$"{QuestPriorityShipment} declinePlayerMessage"] = "Keep it sealed.",
            [$"{QuestPriorityShipment} completePlayerMessage"] = "Open the black rack.",
            [$"{QuestPriorityShipment} changeQuestMessageText"] = "Priority shipment remains held.",
            [q6PmcCondition] = "Eliminate 8 PMCs",
            [q6DogtagCondition] = "Hand over 12 PMC dogtags from level 30+ operators"
        };
    }

    private static List<Item> CollectSubtree(List<Item> allItems, string rootId)
    {
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { rootId };
        var changed = true;
        while (changed)
        {
            changed = false;
            foreach (var item in allItems)
            {
                if (item.ParentId is null || !ids.Contains(item.ParentId))
                {
                    continue;
                }

                if (ids.Add(item.Id.ToString()))
                {
                    changed = true;
                }
            }
        }

        return allItems.Where(x => ids.Contains(x.Id.ToString())).ToList();
    }

    private static bool MatchesAny(string value, IEnumerable<string> keywords)
    {
        foreach (var keyword in keywords)
        {
            if (!string.IsNullOrWhiteSpace(keyword)
                && value.Contains(keyword.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static bool LooksLikeMagazine(string internalName)
    {
        return internalName.Contains("mag", StringComparison.OrdinalIgnoreCase)
               || internalName.Contains("clip", StringComparison.OrdinalIgnoreCase);
    }

    private static string GetPlatformKey(string internalName, IEnumerable<string> keywords)
    {
        foreach (var keyword in keywords)
        {
            if (!string.IsNullOrWhiteSpace(keyword)
                && internalName.Contains(keyword.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                return keyword.Trim().ToLowerInvariant();
            }
        }

        return internalName;
    }

    private static int RoundRoubles(double value)
    {
        var safeValue = Math.Max(1, value);
        var increment = safeValue switch
        {
            >= 100000 => 1000,
            >= 10000 => 100,
            >= 1000 => 50,
            _ => 10
        };

        return (int)(Math.Ceiling(safeValue / increment) * increment);
    }

    private static string StableId(string seed)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(seed));
        return Convert.ToHexString(hash.AsSpan(0, 12)).ToLowerInvariant();
    }

    private sealed record OfferCandidate(
        string SourceTraderId,
        TraderAssort SourceAssort,
        Item Root,
        string InternalName,
        double PriceRoubles,
        int SubtreeCount)
    {
        public string SourceKey => $"{SourceTraderId}:{Root.Id}";
    }

    private sealed record OfferResult(
        MongoId RootId,
        List<Item> Items,
        string InternalName,
        int PriceRoubles);
}
