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
            [new("CustomizePlus.Profile.GetList")],
"""
// Target name
var args = 'self';

function main(profilesJson) {
    const profiles = JSON.parse(profilesJson);
    const macros = profiles.flatMap(p => {
        return [{
            name: `Enable Profile [${p.Item2}]`,
            path: `Customizations/${p.Item2}/Enable`,
            tags: ['customize', 'profile', 'enable'],
            args: args,
            content: `/customize profile enable {0},${p.Item2}`
        },{
            name: `Disable Profile [${p.Item2}]`,
            path: `Customizations/${p.Item2}/Disable`,
            tags: ['customize', 'profile', 'disable'],
            args: args,
            content: `/customize profile disable {0},${p.Item2}`
        }];
    });
    return JSON.stringify(macros);
}
"""),
        new("Custom Emotes",
            [new(PenumbraIpc.MOD_LIST_WITH_SETTINGS), new(EmotesIpc.LIST)],
"""
// Requires "DeterministicPose" and "ModSettingCommands" plugins to be installed

// Recommended to use ModAutoTagger plugin to define the mod bulk tags for the conflict resolution
// Chat filters plugin like for example NoSoliciting can help reduce noise when running commands

// Collection name
var args = 'Self';

const idlePseudoEmote = {
    command: '/idle',
    actionTimelineKeys: [],
    poseKeys: ['emote/pose00_loop', 'emote/pose01_loop', 'emote/pose02_loop', 'emote/pose03_loop', 'emote/pose04_loop', 'emote/pose05_loop', 'emote/pose06_loop']
};

function main(modsJson, emotesJson) {
    const mods = JSON.parse(modsJson);
    const emotes = JSON.parse(emotesJson);

    const macros = mods.flatMap(mod => {
        const optionMacros = mod.settings.groupSettings.flatMap(setting => {
            return setting.options.flatMap(option => {
                const optionGamePaths = Object.keys(option.files || {})
                const optionCommandsWithPoseIndex = lookupCommandsWithPoseIndex(emotes, optionGamePaths);

                return optionCommandsWithPoseIndex.map(([command, poseIndex]) => {
                    const contentLines = [
                        `/penumbra bulktag disable {0} | ${command}`,
                        `/modset {0} "${mod.dir}" "${mod.name}" "${setting.name}" = "${option.name}"`,
                        `/penumbra mod enable {0} | ${mod.dir}`,
                        '/penumbra redraw <me> <wait.1>'
                    ];

                    const commandPath = pushCommandWithPose(contentLines, command, poseIndex);

                    return {
                        name: `Custom Emote [${option.name}] [${commandPath}]`,
                        path: `Mods/${normalize(mod.path)}/Settings/${escape(setting.name)}/Options/${escape(option.name)}/Emotes${commandPath}`,
                        tags: ['mod', 'emote', 'option', command],
                        args: args,
                        content: contentLines.join("\n")
                    };
                });
            });
        })

        if (optionMacros.length > 0) {
            return optionMacros;
        } else {
            const modGamePaths = Object.keys(mod.settings.files || {});
            const modCommandsWithPoseIndex = lookupCommandsWithPoseIndex(emotes, modGamePaths);

            var commandsWithPoseIndex;
            if (modCommandsWithPoseIndex.length > 0) {
                  commandsWithPoseIndex = modCommandsWithPoseIndex;
            } else {
                  const tagCommandsWithPoseIndex = mod.localTags.flatMap(t => t.startsWith('/') ? [[t, -1]] : []);
                  commandsWithPoseIndex = tagCommandsWithPoseIndex;
            }

            return commandsWithPoseIndex.map(([command, poseIndex]) => {
                const contentLines = [
                    `/penumbra bulktag disable {0} | ${command}`,
                    `/penumbra mod enable {0} | ${mod.dir}`,
                    '/penumbra redraw <me> <wait.1>'
                ];

                const commandPath = pushCommandWithPose(contentLines, command, poseIndex);

                return {
                    name: `Custom Emote [${mod.name}] [${commandPath}]`,
                    path: `Mods/${normalize(mod.path)}/Emotes${commandPath}`,
                    tags: ['mod', 'emote', command],
                    args: args,
                    content: contentLines.join("\n")
                };
            });      
        }
    });
    return JSON.stringify(macros);
}

function lookupCommandsWithPoseIndex(emotes, gamePaths) {
    return emotes.concat([idlePseudoEmote]).flatMap(emote => {
        const keys = emote.actionTimelineKeys.concat(emote.poseKeys);
        return keys.flatMap(key => {
            return gamePaths.flatMap(gamePath => {
                if (gamePath.endsWith(`${key}.pap`)) {
                    return [[emote.command, emote.poseKeys.indexOf(key)]]
                } else {
                    return [];
                }
            });
        });
    });
}

function pushCommandWithPose(contentLines, command, poseIndex) {
    if (poseIndex > -1) {
        if (command == idlePseudoEmote.command) {
            contentLines.push(`/dpose ${poseIndex}`);
            return `/idle (${poseIndex})`;
        } else {
            contentLines.push(`${command} <wait.1>`, `/dpose ${poseIndex}`);
            return `${command} (${poseIndex})`;
        }
    } else {
        contentLines.push(command);
        return command;
    }
}

function escape(segment) {
    return segment.replaceAll('/', '|');
}

function normalize(path) {
    return path.replaceAll('\\', '|');
}
"""),
        new("Emotes",
            [new(EmotesIpc.LIST)],
"""
function main(emotesJson) {
    const emotes = JSON.parse(emotesJson);
    const macros = emotes.map(e => {
        return {
            name: e.name,
            path: `Emotes/${e.category[0].toUpperCase()}${e.category.slice(1)}/${e.name}`,
            tags: ['emote', e.category.toLowerCase(), e.command],
            content: e.command
        };
    });
    return JSON.stringify(macros);
}
"""),
        new("Glamours",
            [new(GlamourerIpc.DESIGN_LIST)],
"""
// Recommended to use ModAutoTagger plugin to define the mod bulk tags for the conflict resolution
// Chat filters plugin like for example NoSoliciting can help reduce noise when running commands

var args = 'Self Base';

function main(designsJson) {
    const designs = JSON.parse(designsJson);
    const macros = designs.map(d => {
        const contentLines = [
            '/penumbra bulktag disable {0} | all',
            '/glamour apply {1} | <me>; true',
            `/glamour apply ${d.id} | <me>; true`
        ];

        return {
            name: `Apply Design [${d.name}]`,
            path: `Glamours/${d.path}/Apply`,
            tags: d.tags.concat(['glamour', 'design', 'apply']),
            args: args,
            content: contentLines.join("\n")
        }
    })
    return JSON.stringify(macros);
}
"""),
        new("Honorifics",
            [new("Honorific.GetCharacterTitleList", """["Character Name", WorldId]""")],
"""
// Second IPC argument value (WorldId) can be found as key in %appdata%\xivlauncher\pluginConfigs\Honorific.json

function main(titlesJson) {
    const titles = JSON.parse(titlesJson);

    const disablehonorificsMacro = {
        name: `Disable Honorifics`,
        path: 'Honorifics/Disable',
        tags: ['honorifics', 'titles', 'disable'],
        command: '/disablehonorifics',
        content: titles.map(t => `/honorific title disable ${t.Title}`).join("\n")
    };

    const titleMacros = titles.map(t => {
        const contentLines = [
            `${disablehonorificsMacro.command} <wait.1>`,
            `/honorific title enable ${t.Title}`
        ];
        return {
            name: `Enable Honorific [${t.Title}]`,
            path: `Honorifics/${escape(t.Title)}/Enable`,
            tags: ['honorific', 'title', 'enable'],
            content: contentLines.join("\n")
        };
    });
    return JSON.stringify(titleMacros.concat([disablehonorificsMacro]));
}

function escape(segment) {
    return segment.replaceAll('/', '|');
}
"""),
        new("Jobs",
            [],
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
            name: `Equip Job [${j}]`,
            path: `Jobs/${j}/Equip`,
            tags: ['job', j.toLowerCase(), 'equip'],
            content: `/equipjob ${j}`
        };
    });
    return JSON.stringify(macros);
}
"""),
        new("Macros",
            [new("Quack.Macros.GetList")],
"""
function main(rawMacrosJson) {
    const rawMacros = JSON.parse(rawMacrosJson);
    const macros = rawMacros.flatMap(m => {
        var setName = ['Individual', 'Shared'][m.set];
        return [{
            name: `Macro [${m.name || 'Blank'}]`,
            tags: ['macro', setName.toLowerCase()],
            path: `Macros/${setName}/${m.index}/${m.name}`,
            content: m.content
        }];
    });
    return JSON.stringify(macros);
}
"""),
        new("Mods",
            [new(PenumbraIpc.MOD_LIST)],
"""
// Chat filters plugin like for example NoSoliciting can help reduce noise when running commands

// Collection name
var args = 'Self';

function main(modsJson) {
    const mods = JSON.parse(modsJson);
    const macros = mods.flatMap(m => {
        return [{
            name: `Enable Mod [${m.name}]`,
            path: `Mods/${normalize(m.path)}/Enable`,
            tags: m.localTags.concat(['mod', 'enable']),
            args: args,
            content: `/penumbra mod enable {0} | ${m.dir}`
        },{
            name: `Disable Mod [${m.name}]`,
            path: `Mods/${normalize(m.path)}/Disable`,
            tags: m.localTags.concat(['mod', 'disable']),
            args: args,
            content: `/penumbra mod disable {0} | ${m.dir}`
        }];
    })
    return JSON.stringify(macros);
}

function normalize(path) {
    return path.replaceAll('\\', '|');
}
"""),
        new("Mod Options",
            [new(PenumbraIpc.MOD_LIST_WITH_SETTINGS)],
"""
// Chat filters plugin like for example NoSoliciting can help reduce noise when running commands

// Collection name
var args = 'Self';

function main(modsJson) {
    const mods = JSON.parse(modsJson);

    const macros = mods.flatMap(m => {
        return m.settings.groupSettings.flatMap(s => {
            const groupMacros = [{
                name: `Clear Option Group [${s.name}]`,
                path: `Mods/${m.path}/Settings/${escape(s.name)}/Clear`,
                tags: ['mod', 'options', 'clear'],
                args: args,
                content: `/modset {0} "${m.dir}" "${m.name}" "${s.name}" =`
            }];
            const optionMacros = s.options.map(o => {
                return {
                    name: `Enable Option [${o.name}]`,
                    path: `Mods/${normalize(m.path)}/Settings/${escape(s.name)}/Options/${escape(o.name)}`,
                    tags: ['mod', 'option', 'enable'],
                    args: args,
                    content: `/modset {0} "${m.dir}" "${m.name}" "${s.name}" = "${o.name}"`
                };
            });

            return groupMacros.concat(optionMacros);
        })
    });

    return JSON.stringify(macros);
}

function escape(segment) {
    return segment.replaceAll('/', '|');
}

function normalize(path) {
    return path.replaceAll('\\', '|');
}
"""),
                new("Moodles",
            [new("Moodles.GetRegisteredMoodlesInfo")],
"""
// Target name
var args = 'self';

function main(moodlesJson) {
     var moodles = JSON.parse(moodlesJson);
     var macros = moodles.map(m => {
        return {
            name: `Apply Moodle [${m.Item3}]`,
            path: `Moodles/${m.Item3}/Apply`,
            tags: ['moodle', 'apply'],
            args: args,
            content: `/moodle apply {0} moodle "${m.Item1}"`
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

    public List<GeneratorIpcConfig> IpcConfigs { get; set; } = [];

    [ObsoleteAttribute($"IpcName deprecated to support multiple ipcs in config version 1")]
    public string IpcName { get; set; } = string.Empty;

    [ObsoleteAttribute($"IpcArgs deprecated to support multiple ipcs in config version 1")]
    public string IpcArgs { get; set; } = string.Empty;

    public string Script { get; set; } = string.Empty;

    public GeneratorConfig() { }

    public GeneratorConfig(string name, List<GeneratorIpcConfig> ipcConfigs, string script)
    {
        Name = name;
        IpcConfigs = ipcConfigs;
        Script = script;
    }

    public GeneratorConfig Clone()
    {
        return (GeneratorConfig)MemberwiseClone();
    }
}
