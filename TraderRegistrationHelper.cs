using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Spt.Config;
using SPTarkov.Server.Core.Models.Spt.Tables;
using SPTarkov.Server.Core.Utils.Cloners;

namespace NATOQuartermaster;

[Injectable(TypePriority = OnLoadOrder.TraderRegistration + 1)]
public sealed class TraderRegistrationHelper(
    ICloner cloner,
    TradersTable tradersTable,
    LocaleTable localeTable)
{
    public void SetTraderUpdateTime(
        TraderConfig traderConfig,
        TraderBase traderBase,
        int refreshTimeSecondsMin,
        int refreshTimeSecondsMax)
    {
        traderConfig.UpdateTime.Add(new UpdateTime
        {
            TraderId = traderBase.Id,
            Seconds = new MinMax<int>(refreshTimeSecondsMin, refreshTimeSecondsMax)
        });
    }

    public void AddTraderWithEmptyAssort(TraderBase traderBase)
    {
        var trader = new Trader
        {
            Assort = new TraderAssort
            {
                Items = [],
                BarterScheme = new Dictionary<MongoId, List<List<BarterScheme>>>(),
                LoyalLevelItems = new Dictionary<MongoId, int>()
            },
            Base = cloner.Clone(traderBase),
            QuestAssort = new()
            {
                { "started", new() },
                { "success", new() },
                { "fail", new() }
            },
            Dialogue = []
        };

        tradersTable.TryAdd(traderBase.Id, trader);
    }

    public void AddTraderLocales(TraderBase traderBase, string firstName, string description)
    {
        var traderId = traderBase.Id;
        foreach (var (_, locale) in localeTable.Global)
        {
            locale.AddTransformer(data =>
            {
                data[$"{traderId} FullName"] = traderBase.Name;
                data[$"{traderId} FirstName"] = firstName;
                data[$"{traderId} Nickname"] = traderBase.Nickname;
                data[$"{traderId} Location"] = traderBase.Location;
                data[$"{traderId} Description"] = description;
                return data;
            });
        }
    }

    public void AddLocaleEntries(IReadOnlyDictionary<string, string> entries)
    {
        foreach (var (_, locale) in localeTable.Global)
        {
            locale.AddTransformer(data =>
            {
                foreach (var (key, value) in entries)
                {
                    data[key] = value;
                }

                return data;
            });
        }
    }
}
