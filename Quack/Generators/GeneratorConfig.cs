using Quack.Ipcs;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace Quack.Generators;

[Serializable]
public class GeneratorConfig
{
    private static readonly ImmutableList<GeneratorConfig> DEFAULTS = [
        new("Customize",
            "CustomizePlus.Profile.GetList",
            string.Empty,
"""
function main(listJson) {
    const list = JSON.parse(listJson);
    const macros = list.flatMap(p => {
        return [{
            name: `Enable Profile "${p.Item2}"`,
            path: `Customize/${p.Item2}/Enable`,
            content: `/customize profile enable <me>,${p.Item2}`
        },{
            name: `Disable Profile "${p.Item2}"`,
            path: `Customize/${p.Item2}/Disable`,
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
            path: `Emotes/${e.category[0].toUpperCase()}${e.category.slice(1)}/${e.name}`,
            content: e.command
        };
    });
    return JSON.stringify(macros);
}
"""),
        new("Glamourer",
            GlamourerIpc.DESIGN_LIST,
            string.Empty,
"""
function main(designListJson) {
    const designList = JSON.parse(designListJson);
    const macros = designList.flatMap(d => {
        return [{
            name: `Apply Design "${d.name}" with mods`,
            path: `Glamours/${d.path}/Apply with mods`,
            tags: d.tags,
            content: `/glamour apply ${d.id} | <me>; true`
        }, {
            name: `Apply Design "${d.name}"`,
            path: `Glamours/${d.path}/Apply`,
            tags: d.tags,
            content: `/glamour apply ${d.id} | <me>; false`
        }];
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
    const macros = titleData.formatMap(d => {
        return [{
            name: `Enable Honorific "${d.Title}"`,
            path: `Honorifics/{d.Title}/Enable`,
            content: `/honorific title enable ${d.Title}`
        }, {
            name: `Disable Honorific "${d.Title}"`,
            path: `Honorifics/{d.Title}/Disable`,
            content: `/honorific title disable ${d.Title}`
        }];
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
            path: `Macros/${['Individual', 'Shared'][m.set]}/${[m.index, m.name].filter(Boolean).join('/')}`,
            content: m.content
        }];
    });
    return JSON.stringify(macros);
}
"""),
        new("Penumbra",
            PenumbraIpc.MOD_LIST,
            string.Empty,
"""
function main(modListJson) {
    const modList = JSON.parse(modListJson);
    const macros = modList.flatMap(m => {
        return [{
            name: `Enable Mod "${m.name}"`,
            path: `Mods/${m.path}/Enable`,
            tags: m.localTags,
            content: `/penumbra mod enable Self | ${m.dir}`
        },{
            name: `Disable Mod "${m.name}"`,
            path: `Mods/${m.path}/Disable`,
            tags: m.localTags,
            content: `/penumbra mod disable Self | ${m.dir}`
        }];
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
            name: `Equip Job "${j}"`,
            path: `Jobs/${j}`,
            content: `/equipjob ${j}`
        };
    });
    return JSON.stringify(macros);
}
"""),
];
    public static List<GeneratorConfig> GetDefaults()
    {
        return DEFAULTS.Select(c => c.Clone()).ToList();
    }

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

    public GeneratorConfig Clone()
    {
        return (GeneratorConfig)MemberwiseClone();
    }
}
