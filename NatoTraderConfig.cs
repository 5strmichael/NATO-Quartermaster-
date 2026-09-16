namespace NATOQuartermaster;

public sealed class NatoTraderConfig
{
    public int RefreshMinHours { get; init; } = 1;
    public int RefreshMaxHours { get; init; } = 2;

    // Quartermaster is intentionally a convenience trader: good equipment is available
    // without loyalty levels, but it costs more than the source trader/flea baseline.
    public double PriceMarkup { get; init; } = 1.25;
    public double RestrictedPriceMarkup { get; init; } = 1.35;
    public double UsdToRub { get; init; } = 135;
    public double EurToRub { get; init; } = 150;

    public int WeaponsPerPlatform { get; init; } = 2;
    public int MaxGearOffers { get; init; } = 24;
    public int MaxMagazineOffers { get; init; } = 12;

    public int WeaponStock { get; init; } = 2;
    public int GearStock { get; init; } = 2;
    public int AmmoStock { get; init; } = 180;
    public int MagazineStock { get; init; } = 8;
    public int SupplyStock { get; init; } = 8;

    public int WeaponBuyLimit { get; init; } = 1;
    public int GearBuyLimit { get; init; } = 1;
    public int AmmoBuyLimit { get; init; } = 180;
    public int MagazineBuyLimit { get; init; } = 4;
    public int SupplyBuyLimit { get; init; } = 4;

    public int RestrictedAmmoStock { get; init; } = 120;
    public int RestrictedAmmoBuyLimit { get; init; } = 120;

    public List<string> WeaponKeywords { get; init; } = [];
    public List<string> RestrictedWeaponKeywords { get; init; } = [];
    public List<string> AmmoKeywords { get; init; } = [];
    public List<string> RestrictedAmmoKeywords { get; init; } = [];
    public List<string> MagazineKeywords { get; init; } = [];
    public List<string> GearKeywords { get; init; } = [];
    public List<string> RestrictedVestKeywords { get; init; } = [];
    public List<string> RestrictedHelmetKeywords { get; init; } = [];
}
