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
function main(profilesJson) {
    const profiles = JSON.parse(profilesJson);
    const macros = profiles.flatMap(p => {
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
        new("Custom Emotes",
            PenumbraIpc.MOD_LIST,
            string.Empty,
"""
// Requires "DeterministicPose" plugin to be installed for cpose index support

// Possible to add custom emote commands using "Simple Tweaks" command alias for example:
// /loaf => /quack exec Mods/Poses/Return To Catte Maxwell/Execute

const commandTagPattern = /(?<command>\/\S+)( (?<poseIndex>\d))?( \((?<comment>.+)\))?/;

function main(modsJson) {
    const mods = JSON.parse(modsJson);
    const macros = mods.flatMap(m => {
        return m.localTags.flatMap(commandTag => {
            const match = commandTagPattern.exec(commandTag);
            if (match) {
                const { command, poseIndex, comment } = match.groups;
                const nameSuffix = comment ? ` (${comment})` : '';
                const commandSuffix = poseIndex ? `<wait.1>\n/dpose ${poseIndex}` : '';

                return [{
                    name: `Custom Emote "${m.name}"${nameSuffix}`,
                    path: `Mods/${m.path}/Execute`,
                    tags: m.localTags,
                    content: `/penumbra bulktag disable Self | ${command}
/penumbra mod enable Self | ${m.dir}
/penumbra redraw <me> <wait.1>
${command}${commandSuffix}`
                }];
            } else {
                return [];
            }
        });
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
            tags: [e.command],
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
function main(designsJson) {
    const designs = JSON.parse(designsJson);
    const macros = designs.map(d => {
        return {
            name: `Apply Design "${d.name}"`,
            path: `Glamours/${d.path}/Apply`,
            tags: d.tags,
            content: `/penumbra bulktag disable Self | all
/glamour apply Base | <me>;true
/glamour apply ${d.id} | <me>; true`
        }
    })
    return JSON.stringify(macros);
}
"""),
        new("Honorific",
            "Honorific.GetCharacterTitleList",
            """["Character Name", WorldId]""",
"""
// Second parameter value (WorldId) can be found as key in %appdata%\xivlauncher\pluginConfigs\Honorific.json

function main(titlesJson) {
    const titles = JSON.parse(titlesJson);
    const macros = titles.flatMap(t => {
        return [{
            name: `Enable Honorific "${t.Title}"`,
            path: `Honorifics/${t.Title}/Enable`,
            content: `/honorific title enable ${t.Title}`
        }, {
            name: `Disable Honorific "${t.Title}"`,
            path: `Honorifics/${t.Title}/Disable`,
            content: `/honorific title disable ${t.Title}`
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
        var setName = ['Individual', 'Shared'][m.set];
        return [{
            name: m.name,
            tags: [setName],
            path: `Macros/${setName}/${m.index}/${m.name}`,
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
function main(modsJson) {
    const mods = JSON.parse(modsJson);
    const macros = mods.flatMap(m => {
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
""")
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
