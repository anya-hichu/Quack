using Dalamud.Plugin;
using Dalamud.Plugin.Ipc;
using Dalamud.Utility;
using Lumina.Excel.Sheets;
using Lumina.Excel;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Quack.Ipcs;

public class LifestreamIpc : IDisposable
{
    public static readonly Dictionary<ResidentialAetheryteKind, string> ResidentialDistrictByCity = new()
    {
        [ResidentialAetheryteKind.Gridania] = "Lavender Beds",
        [ResidentialAetheryteKind.Limsa] = "Mist",
        [ResidentialAetheryteKind.Uldah] = "Goblet",
        [ResidentialAetheryteKind.Kugane] = "Shirogane",
        [ResidentialAetheryteKind.Foundation] = "Empyreum",
    };

    public enum ResidentialAetheryteKind
    {
        Uldah = 9,
        Gridania = 2,
        Limsa = 8,
        Foundation = 70,
        Kugane = 111,
    }

    private enum PropertyType
    {
        House = 0,
        Apartment = 1,
    }

    private class AddressBookFileSystem
    {
        [JsonProperty(Required = Required.Always)]
        public Dictionary<string, string> Data { get; set; } = [];
    }

    private class AddressBookEntry
    {
        // Default values are not stored in JSON
        // https://github.com/NightmareXIV/Lifestream/blob/2.3.2.8/Lifestream/Data/AddressBookEntry.cs
        public string Name { get; set; } = string.Empty;
        public int World { get; set; } = 21;
        public ResidentialAetheryteKind City { get; set; } = ResidentialAetheryteKind.Uldah;
        public int Ward { get; set; } = 1;
        public PropertyType PropertyType { get; set; } = PropertyType.House;
        public int Plot { get; set; } = 1;
        public int Apartment { get; set; } = 1;
        public bool ApartmentSubdivision { get; set; } = false;
        public bool AliasEnabled { get; set; } = false;
        public string Alias { get; set; } = string.Empty;
    }

    private class AddressBookFolder
    {
        [JsonProperty(Required = Required.Always)]
        public AddressBookEntry[] Entries { get; set; } = [];

        [JsonProperty(Required = Required.Always)]
        public string GUID { get; set; } = string.Empty;
    }

    private class DefaultConfig
    {
        [JsonProperty(Required = Required.Always)]
        public AddressBookFolder[] AddressBookFolders { get; set; } = [];
    }

    public static readonly string ADDRESS_LIST = "Quack.Lifestream.GetAddressList";

    private ExcelSheet<World> WorldSheet { get; init; }

    private string PluginConfigsDirectory { get; init; }
    private string AddressBookFileSystemPath { get; init; }
    private string DefaultConfigPath { get; init; }

    private ICallGateProvider<Dictionary<string, object>[]> GetAddressListProvider { get; init; }

    public LifestreamIpc(IDalamudPluginInterface pluginInterface, ExcelSheet<World> worldSheet)
    {
        WorldSheet = worldSheet;

        PluginConfigsDirectory = Path.GetFullPath(Path.Combine(pluginInterface.GetPluginConfigDirectory(), ".."));

        // %appdata%\xivlauncher\pluginConfigs\Lifestream\AddressBookFileSystem.json
        AddressBookFileSystemPath = Path.GetFullPath(Path.Combine(PluginConfigsDirectory, "Lifestream\\AddressBookFileSystem.json"));

        // %appdata%\xivlauncher\pluginConfigs\Lifestream\DefaultConfig.json
        DefaultConfigPath = Path.GetFullPath(Path.Combine(PluginConfigsDirectory, "Lifestream\\DefaultConfig.json"));

        GetAddressListProvider = pluginInterface.GetIpcProvider<Dictionary<string, object>[]>(ADDRESS_LIST);
        GetAddressListProvider.RegisterFunc(GetAddressList);
    }

    public void Dispose()
    {
        GetAddressListProvider.UnregisterFunc();
    }

    private Dictionary<string, object>[] GetAddressList()
    {
        if (!File.Exists(AddressBookFileSystemPath))
        {
            throw new FileNotFoundException($"Failed to find lifestream address book file system file at {AddressBookFileSystemPath}");
        }

        if (!File.Exists(DefaultConfigPath))
        {
            throw new FileNotFoundException($"Failed to find lifestream default config path file at {DefaultConfigPath}");
        }

        using StreamReader addressBookFileSystemReader = new(AddressBookFileSystemPath);
        var addressBookFileSystemJson = addressBookFileSystemReader.ReadToEnd();
        var addressBookFileSystem = JsonConvert.DeserializeObject<AddressBookFileSystem>(addressBookFileSystemJson)!;

        using StreamReader defaultConfigReader = new(DefaultConfigPath);
        var defaultConfigJson = defaultConfigReader.ReadToEnd();
        var defaultConfig = JsonConvert.DeserializeObject<DefaultConfig>(defaultConfigJson)!;

        return defaultConfig.AddressBookFolders.SelectMany(addressBookFolder =>
        {
            var folder = addressBookFileSystem.Data.GetValueOrDefault(addressBookFolder.GUID, string.Empty);

            return addressBookFolder.Entries.Select(entry =>
            {
                var residentialDistrict = ResidentialDistrictByCity[entry.City];
                return new Dictionary<string, object>() {
                        { "name", entry.Name },
                        { "world", WorldSheet.First(w => w.RowId == entry.World).Name.ToString() },
                        { "city", residentialDistrict }, // TODO: Remove (misnamed)
                        { "residentialDistrict", residentialDistrict },
                        { "ward", entry.Ward },
                        { "propertyType", entry.PropertyType.ToString() },
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
