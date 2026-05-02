using Dalamud.Plugin;
using Dalamud.Plugin.Ipc;
using Dalamud.Utility;
using Lumina.Excel;
using Lumina.Excel.Sheets;
using System;
using System.Collections.Generic;
using System.Linq;

using AddressBookEntryTuple = (string Name, int World, int City, int Ward, int PropertyType, int Plot, int Apartment, bool ApartmentSubdivision, bool AliasEnabled, string Alias);

namespace Quack.Ipcs;

public class LifestreamIpc : IDisposable
{
    public static readonly Dictionary<int, string> ResidentialDistrictByCityId = new()
    {
        { 2, "Lavender Beds" },
        { 8, "Mist" },
        { 9, "Goblet" },
        { 111, "Shirogane" },
        { 70, "Empyreum" }
    };

    private static readonly Dictionary<int, string> PropertyTypeById = new()
    {
        { 0, "House" },
        { 1, "Apartment" }
    };

    public static readonly string ADDRESS_LIST = "Quack.Lifestream.GetAddressList";

    private ExcelSheet<World> WorldSheet { get; init; }

    private ICallGateSubscriber<Dictionary<string, List<AddressBookEntryTuple>>> GetAddressBookEntriesWithFolders { get; init; }

    private ICallGateProvider<Dictionary<string, object>[]> GetAddressListProvider { get; init; }

    public LifestreamIpc(IDalamudPluginInterface pluginInterface, ExcelSheet<World> worldSheet)
    {
        WorldSheet = worldSheet;

        GetAddressBookEntriesWithFolders = pluginInterface.GetIpcSubscriber<Dictionary<string, List<AddressBookEntryTuple>>>("Lifestream.GetAddressBookEntriesWithFolders");

        GetAddressListProvider = pluginInterface.GetIpcProvider<Dictionary<string, object>[]>(ADDRESS_LIST);
        GetAddressListProvider.RegisterFunc(GetAddressList);
    }

    public void Dispose()
    {
        GetAddressListProvider.UnregisterFunc();
    }

    private Dictionary<string, object>[] GetAddressList()
    {
        var addressBookEntriesWithFolders = GetAddressBookEntriesWithFolders.InvokeFunc();

        return addressBookEntriesWithFolders.SelectMany(kv =>
        {
            var (folder, entries) = kv;

            return entries.Select(entry =>
            {
                var residentialDistrict = ResidentialDistrictByCityId[entry.City];
                return new Dictionary<string, object>() {
                        { "name", entry.Name },
                        { "world", WorldSheet.First(w => w.RowId == entry.World).Name.ToString() },
                        { "city", residentialDistrict },
                        { "residentialDistrict", residentialDistrict },
                        { "ward", entry.Ward },
                        { "propertyType", PropertyTypeById[entry.PropertyType] },
                        { "plot", entry.Plot },
                        { "apartment", entry.Apartment },
                        { "apartmentSubdivision", entry.ApartmentSubdivision },
                        { "aliasEnabled", entry.AliasEnabled },
                        { "alias", entry.Alias },
                        { "path", folder.IsNullOrWhitespace() ? entry.Name : string.Join('/', [folder, entry.Name]) }
                    };
            });
        }).ToArray();
    }
}
