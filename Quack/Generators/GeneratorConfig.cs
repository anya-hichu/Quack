using Quack.Ipcs;
using System;
using System.Collections.Immutable;

namespace Quack.Generators;

[Serializable]
public class GeneratorConfig
{
    public static readonly ImmutableList<GeneratorConfig> DEFAULTS = [
        new("Customize",
            "CustomizePlus.Profile.GetList",
            string.Empty,
"""
function main(listJson) {
    const list = JSON.parse(listJson);
    const macros = list.flatMap(p => {
        return [{
            name: `Enable profile "${p.Item2}"`,
            path: "profiles",
            content: `/customize profile enable <me>,${p.Item2}`
        },{
            name: `Disable profile "${p.Item2}"`,
            path: "profiles",
            content: `/customize profile disable <me>,${p.Item2}`
        }];
    });
    return JSON.stringify(macros);
}
"""),
        new("Emotes",
            EmotesIpc.LIST,
            string.Empty,
"""
function main(emotesJson) {
    const emotes = JSON.parse(emotesJson);
    const macros = emotes.map(e => {
        return {
            name: e.name,
            path: `emotes/${e.category.toLowerCase()}`,
            content: e.command
        };
    });
    return JSON.stringify(macros);
}
"""),
        new("Glamourer",
            "Glamourer.GetDesignList.V2",
            string.Empty,
"""
function main(designListJson) {
    const designList = JSON.parse(designListJson);
    const macros = Object.entries(designList).map(([id, name]) => {
        return {
            name: `Apply design "${name}"`,
            path: 'glamours',
            content: `/glamour apply ${id} | <me>; true`
        };
    })
    return JSON.stringify(macros);
}
"""),
        new("Honorific",
            "Honorific.GetCharacterTitleList",
            """["Character Name", WorldId]""",
"""
// Second parameter value (WorldId) can be found as key in %appdata%\xivlauncher\pluginConfigs\Honorific.json

function main(titleDataJson) {
    const titleData = JSON.parse(titleDataJson);
    const macros = titleData.map(d => {
        return {
            name: `Enable title "${d.Title}"`,
            path: "honorifics",
            content: `/honorific title enable ${d.Title}`
        };
    });
    return JSON.stringify(macros);
}
"""),
        new("Macros",
            "Quack.Macros.GetList",
            string.Empty,
"""
function main(rawMacrosJson) {
    const rawMacros = JSON.parse(rawMacrosJson);
    const macros = rawMacros.flatMap(m => {
        return [{
            name: m.name,
            path: `macros/${m.set}/${m.index}`,
            content: m.content
        }];
    });
    return JSON.stringify(macros);
}
"""),
        new("Penumbra",
            "Penumbra.GetModList",
            string.Empty,
"""
function main(modListJson) {
    const modList = JSON.parse(modListJson);
    const macros = [];
    Object.entries(modList).forEach(([path, name]) => {
        macros.push({
            name: `Enable mod "${name}"`,
            path: 'mods',
            content: `/penumbra mod enable Self | ${name}`
        });
        macros.push({
            name: `Disable mod "${name}"`,
            path: 'mods',
            content: `/penumbra mod disable Self | ${name}`
        });
    })
    return JSON.stringify(macros);
}

"""),
        new("Simple Tweaks",
            string.Empty,
            string.Empty,
"""
// Requires Simple Tweak > Command > Equip Job Command to be enabled

const jobs = [
    'ARC', 'ACN', 'CNJ', 'GLA', 'LNC', 'MRD', 'PGL', 'ROG', 'THM',
    'ALC', 'ARM', 'BSM', 'CUL', 'CRP', 'GSM', 'LTW', 'WVR',
    'BTN', 'FSH', 'MIN',
    'BLM', 'BRD', 'DRG', 'MNK', 'NIN', 'PLD', 'SCH', 'SMN', 'WAR', 'WHM', 'SAM', 'RDM', 'MCH', 'DRK', 'AST', 'GNB', 'DNC', 'SGE', 'RPR', 'VPR', 'PTN', 'BLU'
];

function main() {
    const macros = jobs.map(j => {
        return {
            name: `Equip job "${j}"`,
            path: 'jobs',
            content: `/equipjob ${j}`
        };
    });
    return JSON.stringify(macros);
}
"""),
];

    public string Name { get; set; } = string.Empty;
    public string IpcName { get; set; } = string.Empty;
    public string IpcArgs { get; set; } = string.Empty;
    public string Script { get; set; } = string.Empty;

    public GeneratorConfig() { }

    public GeneratorConfig(string name, string ipcName, string ipcArgs, string script)
    {
        Name = name;
        IpcName = ipcName;
        IpcArgs = ipcArgs;
        Script = script;
    }
}
