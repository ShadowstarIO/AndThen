using System;
using FFXIVClientStructs.FFXIV.Client.Game;
using Lumina.Excel.Sheets;

namespace AndThen;

public enum ResidenceKind
{
    None = 0,
    House = 1,
    Apartment = 2,
    Chamber = 3,
}

public readonly record struct HousingAddress(
    ResidenceKind Kind,
    string District,
    int Ward,
    int Plot,
    int Room,
    bool Subdivision)
{
    public string Summary
    {
        get
        {
            var place = District.Length == 0 ? "Unknown district" : District;
            var div = Subdivision ? " sub" : "";
            return Kind switch
            {
                ResidenceKind.House => $"{place}{div}  W{Ward}  P{Plot}",
                ResidenceKind.Apartment => $"{place}{div}  W{Ward}" + (Room > 0 ? $"  Apt {Room}" : "  Apt"),
                ResidenceKind.Chamber => $"{place}{div}  W{Ward}  P{Plot}" + (Room > 0 ? $"  Room {Room}" : ""),
                _ => Ward is >= 1 and <= 30 ? $"{place}{div}  W{Ward}  (not on a plot)" : "Not at a housing address",
            };
        }
    }
}

internal static class Housing
{
    public static readonly string[] Districts =
    [
        "Mist",
        "The Lavender Beds",
        "The Goblet",
        "Shirogane",
        "Empyreum",
    ];

    public static readonly string[] Kinds = ["House", "Apartment", "Chamber"];

    public static HousingAddress Read(uint territoryId)
    {
        try
        {
            unsafe
            {
                var manager = HousingManager.Instance();
                if (manager == null) return default;
                return Read(manager, territoryId);
            }
        }
        catch (Exception ex)
        {
            Plugin.Log.Verbose(ex, "Housing read failed");
            return default;
        }
    }

    public static string Encode(int kindIndex, int zoneIndex, bool subdivision, int ward, int plot, int room)
    {
        var type = kindIndex switch
        {
            1 => "Apartment",
            2 => "FcApartment",
            _ => "House",
        };
        var district = zoneIndex > 0 && zoneIndex <= Districts.Length ? Districts[zoneIndex - 1] : string.Empty;
        return Encode(type, district, ward, plot, room, subdivision);
    }

    public static string Encode(HousingAddress here)
    {
        if (here.Kind == ResidenceKind.None) return string.Empty;
        var type = here.Kind switch
        {
            ResidenceKind.Apartment => "Apartment",
            ResidenceKind.Chamber => "FcApartment",
            _ => "House",
        };
        return Encode(type, here.District, here.Ward, here.Plot, here.Room, here.Subdivision);
    }

    public static string Format(string value)
    {
        if (!TryParse(value, out var type, out var district, out var ward, out var plot, out var room, out var sub))
            return "Housing " + value;
        var place = district.Length == 0 ? "any district" : district;
        var div = sub ? "sub" : "main";
        var wardText = ward > 0 ? $"W{ward}" : "any ward";
        return type switch
        {
            "Apartment" => $"Apt {place} {div} {wardText}" + (room > 0 ? $" #{room}" : ""),
            "FcApartment" => $"Chamber {place} {div} {wardText}" + (plot > 0 ? $" P{plot}" : "") + (room > 0 ? $" R{room}" : ""),
            _ => $"House {place} {div} {wardText}" + (plot > 0 ? $" P{plot}" : ""),
        };
    }

    public static bool Matches(string value, HousingAddress here)
    {
        if (here.Kind == ResidenceKind.None) return false;
        if (!TryParse(value, out var type, out var district, out var ward, out var plot, out var room, out var sub))
            return false;
        if (type == "House" && here.Kind != ResidenceKind.House) return false;
        if (type == "Apartment" && here.Kind != ResidenceKind.Apartment) return false;
        if (type == "FcApartment" && here.Kind != ResidenceKind.Chamber) return false;
        if (!DistrictMatch(here.District, district)) return false;
        if (ward > 0 && here.Ward != ward) return false;
        if (sub != here.Subdivision) return false;
        if (plot > 0 && here.Plot != plot) return false;
        if (room > 0 && here.Room != room) return false;
        return true;
    }

    public static int ZoneIndex(string district)
    {
        if (string.IsNullOrWhiteSpace(district)) return 0;
        for (var i = 0; i < Districts.Length; i++)
            if (DistrictMatch(Districts[i], district)) return i + 1;
        return 0;
    }

    private static unsafe HousingAddress Read(HousingManager* manager, uint territoryId)
    {
        var wardRaw = manager->GetCurrentWard();
        var plotRaw = manager->GetCurrentPlot();
        var division = manager->GetCurrentDivision();
        var roomRaw = manager->GetCurrentRoom();
        var houseId = manager->GetCurrentHouseId();

        var ward = wardRaw >= 0 ? wardRaw + 1 : houseId.Id != 0 ? houseId.WardIndex + 1 : 0;
        if (ward is < 1 or > 30) return default;

        var wing = IsWing(territoryId) || (houseId.Id != 0 && houseId.IsApartment && manager->IsInside());
        var chamber = IsChamber(territoryId);
        var workshop = IsWorkshop(territoryId) || manager->IsInWorkshop() || houseId.IsWorkshop || roomRaw == 0x3FF;
        var interior = IsHouseInterior(territoryId);
        var outdoor = IsOutdoor(territoryId);
        var inside = manager->IsInside();
        if (!wing && !chamber && !workshop && !interior && !outdoor && !inside && !manager->IsOutside())
            return default;

        var district = DistrictOf(territoryId);
        if (district.Length == 0)
            district = DistrictOf(OriginalTerritory());
        if (district.Length == 0)
            district = DistrictFromNames(territoryId);
        if (district.Length == 0)
            district = DistrictFromNames(OriginalTerritory());

        var subdivision = division == 2 || plotRaw == -127;
        if (inside && houseId.IsApartment)
            subdivision = houseId.ApartmentDivision == 1;

        var room = roomRaw is > 0 and < 0x3FF ? roomRaw : 0;

        // Plot is 0-based on a house. -128 / -127 mark an apartment wing, not a plot.
        if (wing)
            return new HousingAddress(ResidenceKind.Apartment, district, ward, 0, room, subdivision);

        if (plotRaw < 0 || plotRaw > 59)
            return new HousingAddress(ResidenceKind.None, district, ward, 0, 0, subdivision);

        var plot = plotRaw + 1;
        if (chamber && !workshop)
            return new HousingAddress(ResidenceKind.Chamber, district, ward, plot, room, subdivision);

        return new HousingAddress(ResidenceKind.House, district, ward, plot, 0, subdivision);
    }

    private static uint OriginalTerritory()
    {
        try
        {
            unsafe { return HousingManager.GetOriginalHouseTerritoryTypeId(); }
        }
        catch
        {
            return 0;
        }
    }

    private static string Encode(string type, string district, int ward, int plot, int room, bool subdivision)
    {
        ward = Math.Clamp(ward, 0, 30);
        plot = Math.Clamp(plot, 0, 60);
        room = Math.Clamp(room, 0, 9999);
        var tail = subdivision ? "|sub" : "";
        if (type == "Apartment") return $"Apartment|{district}|{ward}|{room}{tail}";
        if (type == "FcApartment") return $"FcApartment|{district}|{ward}|{plot}|{room}{tail}";
        return $"House|{district}|{ward}|{plot}{tail}";
    }

    private static bool TryParse(string value, out string type, out string district, out int ward, out int plot, out int room, out bool sub)
    {
        type = "House";
        district = string.Empty;
        ward = plot = room = 0;
        sub = false;
        var parts = (value ?? string.Empty).Split('|', StringSplitOptions.TrimEntries);
        if (parts.Length < 2) return false;
        type = parts[0] switch
        {
            "Apartment" or "apartment" => "Apartment",
            "FcApartment" or "FC Apartment" or "Chamber" or "chamber" => "FcApartment",
            "House" or "house" => "House",
            _ => string.Empty,
        };
        if (type.Length == 0) return false;
        district = Canon(parts[1]);
        if (parts.Length > 2) int.TryParse(parts[2], out ward);
        if (type == "Apartment")
        {
            if (parts.Length > 3 && parts[3] != "sub") int.TryParse(parts[3], out room);
        }
        else if (parts.Length > 3 && parts[3] != "sub")
        {
            int.TryParse(parts[3], out plot);
            if (type == "FcApartment" && parts.Length > 4 && parts[4] != "sub")
                int.TryParse(parts[4], out room);
        }

        sub = parts[^1].Equals("sub", StringComparison.OrdinalIgnoreCase);
        return true;
    }

    private static bool DistrictMatch(string here, string chip)
    {
        if (string.IsNullOrWhiteSpace(chip)) return true;
        if (string.IsNullOrWhiteSpace(here)) return false;
        var left = Canon(here);
        var right = Canon(chip);
        if (left.Length == 0 || right.Length == 0) return false;
        return left.Contains(right, StringComparison.OrdinalIgnoreCase)
            || right.Contains(left, StringComparison.OrdinalIgnoreCase);
    }

    private static string Canon(string value)
    {
        value = (value ?? string.Empty).Trim();
        if (value.Length == 0) return string.Empty;
        if (Has(value, "Topmast") || Has(value, "Mist")) return "Mist";
        if (Has(value, "Lavender") || Has(value, "Lily Hills")) return "The Lavender Beds";
        if (Has(value, "Goblet") || Has(value, "Sultana")) return "The Goblet";
        if (Has(value, "Shirogane") || Has(value, "Kobai")) return "Shirogane";
        if (Has(value, "Empyreum") || Has(value, "Ingleside")) return "Empyreum";
        return value;
    }

    private static string DistrictOf(uint id) => id switch
    {
        339 or 282 or 283 or 284 or 384 or 423 or 573 or 608 => "Mist",
        340 or 342 or 343 or 344 or 385 or 425 or 574 or 609 => "The Lavender Beds",
        341 or 345 or 346 or 347 or 386 or 424 or 575 or 610 => "The Goblet",
        641 or 649 or 650 or 651 or 652 or 653 or 654 or 655 => "Shirogane",
        979 or 980 or 981 or 982 or 983 or 984 or 985 or 999 => "Empyreum",
        _ => string.Empty,
    };

    private static bool IsOutdoor(uint id) => id is 339 or 340 or 341 or 641 or 979;

    private static bool IsWing(uint id) => id is 573 or 574 or 575 or 608 or 609 or 610 or 654 or 655 or 985 or 999;

    private static bool IsChamber(uint id) => id is 384 or 385 or 386 or 652 or 983;

    private static bool IsWorkshop(uint id) => id is 423 or 424 or 425 or 653 or 984;

    private static bool IsHouseInterior(uint id) => id is 282 or 283 or 284 or 342 or 343 or 344 or 345 or 346 or 347
        or 649 or 650 or 651 or 980 or 981 or 982
        or 1249 or 1250 or 1251 or 1374 or 1375 or 1376;

    private static string DistrictFromNames(uint territoryId)
    {
        if (territoryId == 0) return string.Empty;
        try
        {
            var row = Plugin.DataManager.GetExcelSheet<TerritoryType>().GetRowOrDefault(territoryId);
            if (row == null) return string.Empty;
            var blob = string.Join(' ',
                row?.PlaceName.ValueNullable?.Name.ToString(),
                row?.PlaceNameZone.ValueNullable?.Name.ToString(),
                row?.PlaceNameRegion.ValueNullable?.Name.ToString());
            return Canon(blob);
        }
        catch
        {
            return string.Empty;
        }
    }

    private static bool Has(string hay, string needle) =>
        hay.Contains(needle, StringComparison.OrdinalIgnoreCase);
}
