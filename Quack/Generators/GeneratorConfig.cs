using Quack.Ipcs;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace Quack.Generators;

[Serializable]
public class GeneratorConfig
{
    public static readonly int DEFAULTS_VERSION = 4;
    private static readonly ImmutableList<GeneratorConfig> DEFAULTS = [
        new($"Customize (V{DEFAULTS_VERSION})",
            [new("CustomizePlus.Profile.GetList")],
"""
// Target name
var ARGS = 'self';

function main(profilesJson) {
    const profiles = JSON.parse(profilesJson);
    const macros = profiles.flatMap(p => {
        return [{
            name: `Enable Profile [${p.Item2}]`,
            path: `Customizations/${p.Item2}/Enable`,
            tags: ['customize', 'profile', 'enable'],
            args: ARGS,
            content: `/customize profile enable {0},${p.Item2}`
        },{
            name: `Disable Profile [${p.Item2}]`,
            path: `Customizations/${p.Item2}/Disable`,
            tags: ['customize', 'profile', 'disable'],
            args: ARGS,
            content: `/customize profile disable {0},${p.Item2}`
        }];
    });
    return JSON.stringify(macros);
}
"""),
        new($"Custom Emotes (V{DEFAULTS_VERSION})",
            [new(PenumbraIpc.MOD_LIST_WITH_SETTINGS), new(EmotesIpc.LIST)],
"""
// Requires "DeterministicPose" and "ModSettingCommands" plugins to be installed

// Recommended to use ModAutoTagger plugin to define the mod bulk tags for the conflict resolution
// Chat filters plugin like for example NoSoliciting can help reduce noise when running commands

// Collection name
var ARGS = 'Self';

const IDLE_PSEUDO_EMOTE = {
    command: '/idle',
    actionTimelineKeys: [],
    poseKeys: ['emote/pose00_loop', 'emote/pose01_loop', 'emote/pose02_loop', 'emote/pose03_loop', 'emote/pose04_loop', 'emote/pose05_loop', 'emote/pose06_loop']
};

const RESET_POSITION_MACRO = {
    name: `Reset Position`,
    path: 'Macros/Customs/Reset Position',
    tags: ['reset', 'position', 'macro', 'custom'],
    command: '/resetposition',
    content: ['/ifinthatposition -v -$', '/standup <wait.1>'].join("\n")
};

function main(modsJson, emotesJson) {
    const mods = JSON.parse(modsJson);
    const emotes = JSON.parse(emotesJson);

    const customEmoteMacros = mods.flatMap(mod => {
        const modGamePaths = Object.keys(mod.settings.files || {});
        const modCommandsWithPoseIndex = lookupCommandsWithPoseIndex(emotes, modGamePaths);

        const modMacros = modCommandsWithPoseIndex.map(([command, poseIndex]) => {
            const emoteCommands = buildCommands(command, poseIndex);
            const contentLines = [
                `${RESET_POSITION_MACRO.command} <wait.macro>`,
                `/ifmodset -e -$ {0} "${mod.dir}" "${mod.name}" ; ${emoteCommands.map(escapeCommand).join(' ')}`,
                `/penumbra bulktag disable {0} | ${command}`,
                `/penumbra mod enable {0} | ${mod.dir}`,
                '/penumbra redraw <me> <wait.2>'
            ].concat(emoteCommands);
            const commandPath = buildCommandPath(command, poseIndex);
            return {
                name: `Custom Emote [${mod.name}] [${commandPath}]`,
                path: `Mods/${normalize(mod.path)}/Emotes${commandPath}`,
                tags: ['mod', 'emote', command],
                args: ARGS,
                content: contentLines.join("\n")
            };
        });

        const optionMacros = mod.settings.groupSettings.flatMap(setting => {
            return setting.options.flatMap(option => {
                const optionGamePaths = Object.keys(option.files || {})
                const optionCommandsWithPoseIndex = lookupCommandsWithPoseIndex(emotes, optionGamePaths);

                return optionCommandsWithPoseIndex.map(([command, poseIndex]) => {
                    const emoteCommands = buildCommands(command, poseIndex);
                    const contentLines = [
                        `${RESET_POSITION_MACRO.command} <wait.macro>`,
                        `/ifmodset -e -$ {0} "${mod.dir}" "${mod.name}" "${setting.name}" == "${option.name}" ; ${emoteCommands.map(escapeCommand).join(' ')}`,
                        `/penumbra bulktag disable {0} | ${command}`,
                        `/modset {0} "${mod.dir}" "${mod.name}" "${setting.name}" = "${option.name}"`,
                        `/penumbra mod enable {0} | ${mod.dir}`,
                        '/penumbra redraw <me> <wait.1>'
                    ].concat(emoteCommands);
                    const commandPath = buildCommandPath(command, poseIndex); 
                    return {
                        name: `Custom Emote [${option.name}] [${commandPath}]`,
                        path: `Mods/${normalize(mod.path)}/Settings/${escape(setting.name)}/Options/${escape(option.name)}/Emotes${commandPath}`,
                        tags: ['mod', 'emote', 'option', command],
                        args: ARGS,
                        content: contentLines.join("\n")
                    };
                });
            });
        })

        return modMacros.concat(optionMacros);
    });

    const macros = [RESET_POSITION_MACRO].concat(customEmoteMacros);

    return JSON.stringify(macros);
}

function lookupCommandsWithPoseIndex(emotes, gamePaths) {
    return emotes.concat([IDLE_PSEUDO_EMOTE]).flatMap(emote => {
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

function buildCommands(command, poseIndex) {
    if (poseIndex > -1) {
        if (command == IDLE_PSEUDO_EMOTE.command) {
            return [`/dpose ${poseIndex}`];
        } else {
            return [`${command} motion <wait.1>`, `/dpose ${poseIndex}`];
        }
    } else {
        return [`${command} motion`];
    }
}

function buildCommandPath(command, poseIndex) {
    if (poseIndex > -1) {
        if (command == IDLE_PSEUDO_EMOTE.command) {
            return `/idle ${poseIndex}`;
        } else {
            return `${command} (${poseIndex})`;
        }
    } else {
        return command;
    }
}

function escape(segment) {
    return segment.replaceAll('/', '|');
}

function escapeCommand(command) {
    return `"${command.replaceAll('[', '[[').replaceAll(']', ']]').replaceAll('<', '[').replaceAll('>', ']')}"`
}

function normalize(path) {
    return path.replaceAll('\\', '|');
}
"""),
        new($"Emotes (V{DEFAULTS_VERSION})",
            [new(EmotesIpc.LIST)],
"""
function main(emotesJson) {
    const emotes = JSON.parse(emotesJson);
    const macros = emotes.flatMap(e => {
        var categoryName = `${e.category[0].toUpperCase()}${e.category.slice(1)}`;
        return [{
            name: `Emote [${e.name}]`,
            path: `Emotes/${categoryName}/${e.name}/Execute`,
            tags: ['emote', e.category.toLowerCase(), e.command],
            content: e.command
        }, {
            name: `Emote [${e.name}] [Motion]`,
            path: `Emotes/${categoryName}/${e.name}/Execute [motion]`,
            tags: ['emote', e.category.toLowerCase(), e.command],
            content: `${e.command} motion`
        }];
    });
    return JSON.stringify(macros);
}
"""),
        new($"Glamours (V{DEFAULTS_VERSION})",
            [new(GlamourerIpc.DESIGN_LIST)],
"""
// Recommended to use ModAutoTagger plugin to define the mod bulk tags for the conflict resolution
// Chat filters plugin like for example NoSoliciting can help reduce noise when running commands

var ARGS = 'Self';

function main(designsJson) {
    const designs = JSON.parse(designsJson);
    const macros = designs.map(d => {
        const contentLines = [
            '/penumbra bulktag disable {0} | all',
            `/glamour apply ${d.id} | <me>; true`
        ];

        return {
            name: `Apply Design [${d.name}]`,
            path: `Glamours/${d.path}/Apply`,
            tags: d.tags.concat(['glamour', 'design', 'apply']),
            args: ARGS,
            content: contentLines.join("\n")
        }
    })
    return JSON.stringify(macros);
}
"""),
        new($"Honorifics (V{DEFAULTS_VERSION})",
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
            `${disablehonorificsMacro.command} <wait.macro>`,
            `/honorific title enable ${t.Title}`
        ];
        return {
            name: `Enable Honorific [${t.Title}]`,
            path: `Honorifics/${escape(t.Title)}/Enable`,
            tags: ['honorific', 'title', 'enable'],
            content: contentLines.join("\n")
        };
    });

    const macros = [disablehonorificsMacro].concat(titleMacros);

    return JSON.stringify(macros);
}

function escape(segment) {
    return segment.replaceAll('/', '|');
}
"""),
        new($"Jobs (V{DEFAULTS_VERSION})",
            [],
"""
// Requires Simple Tweak > Command > Equip Job Command to be enabled

const JOBS = [
    'ARC', 'ACN', 'CNJ', 'GLA', 'LNC', 'MRD', 'PGL', 'ROG', 'THM',
    'ALC', 'ARM', 'BSM', 'CUL', 'CRP', 'GSM', 'LTW', 'WVR',
    'BTN', 'FSH', 'MIN',
    'BLM', 'BRD', 'DRG', 'MNK', 'NIN', 'PLD', 'SCH', 'SMN', 'WAR', 'WHM', 'SAM', 'RDM', 'MCH', 'DRK', 'AST', 'GNB', 'DNC', 'SGE', 'RPR', 'VPR', 'PTN', 'BLU'
];

function main() {
    const macros = JOBS.map(j => {
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
        new($"Macros (V{DEFAULTS_VERSION})",
            [new(MacrosIpc.LIST), new(LocalPlayerIpc.INFO)],
"""
function main(rawMacrosJson, localPlayerInfoJson) {
    const rawMacros = JSON.parse(rawMacrosJson);
    const localPlayerInfo = JSON.parse(localPlayerInfoJson);

    const macros = rawMacros.flatMap(m => {
        const name = `Macro [${m.name || 'Blank'}]`;
        if (m.set == 0) {
            return [{
                name: name,
                tags: ['individual', 'macro', `${m.index}`],
                path: `Macros/Individual/${localPlayerInfo.name}/${m.index}/${m.name}`,
                content: m.content
            }];
        } else {
            return [{
                name: name,
                tags: ['shared', 'macro', `${m.index}`],
                path: `Macros/Shared/${m.index}/${m.name}`,
                content: m.content
            }];
        }     
    });
    return JSON.stringify(macros);
}
"""),
        new($"Mods (V{DEFAULTS_VERSION})",
            [new(PenumbraIpc.MOD_LIST)],
"""
// Chat filters plugin like for example NoSoliciting can help reduce noise when running commands

// Collection name
var ARGS = 'Self';

function main(modsJson) {
    const mods = JSON.parse(modsJson);
    const macros = mods.flatMap(m => {
        return [{
            name: `Enable Mod [${m.name}]`,
            path: `Mods/${normalize(m.path)}/Enable`,
            tags: m.localTags.concat(['mod', 'enable']),
            args: ARGS,
            content: `/penumbra mod enable {0} | ${m.dir}`
        },{
            name: `Disable Mod [${m.name}]`,
            path: `Mods/${normalize(m.path)}/Disable`,
            tags: m.localTags.concat(['mod', 'disable']),
            args: ARGS,
            content: `/penumbra mod disable {0} | ${m.dir}`
        }];
    })
    return JSON.stringify(macros);
}

function normalize(path) {
    return path.replaceAll('\\', '|');
}
"""),
        new($"Mod Options (V{DEFAULTS_VERSION})",
            [new(PenumbraIpc.MOD_LIST_WITH_SETTINGS)],
"""
// Chat filters plugin like for example NoSoliciting can help reduce noise when running commands

// Collection name
var ARGS = 'Self';

function main(modsJson) {
    const mods = JSON.parse(modsJson);

    const macros = mods.flatMap(m => {
        return m.settings.groupSettings.flatMap(s => {
            var isMulti = s.type == "Multi"
            const groupMacros = isMulti ? [{
                name: `Clear Option Group [${s.name}]`,
                path: `Mods/${m.path}/Settings/${escape(s.name)}/Clear`,
                tags: ['mod', 'options', 'clear'],
                args: ARGS,
                content: `/modset {0} "${m.dir}" "${m.name}" "${s.name}" =`
            }] : []; 
            const optionMacros = s.options.flatMap(o => {
                if (isMulti) {
                    return [{
                        name: `Enable Exclusively Option [${o.name}]`,
                        path: `Mods/${normalize(m.path)}/Settings/${escape(s.name)}/Options/${escape(o.name)}/Enable [exclusive]`,
                        tags: ['mod', 'option', 'enable', 'exclusive'],
                        args: ARGS,
                        content: `/modset {0} "${m.dir}" "${m.name}" "${s.name}" = "${o.name}"`
                    }, {
                        name: `Enable Option [${o.name}]`,
                        path: `Mods/${normalize(m.path)}/Settings/${escape(s.name)}/Options/${escape(o.name)}/Enable`,
                        tags: ['mod', 'option', 'enable'],
                        args: ARGS,
                        content: `/modset {0} "${m.dir}" "${m.name}" "${s.name}" += "${o.name}"`
                    }, {
                        name: `Disable Option [${o.name}]`,
                        path: `Mods/${normalize(m.path)}/Settings/${escape(s.name)}/Options/${escape(o.name)}/Disable`,
                        tags: ['mod', 'option', 'disable'],
                        args: ARGS,
                        content: `/modset {0} "${m.dir}" "${m.name}" "${s.name}" -= "${o.name}"`
                    }];
                } else {
                    return [{
                        name: `Enable Option [${o.name}]`,
                        path: `Mods/${normalize(m.path)}/Settings/${escape(s.name)}/Options/${escape(o.name)}/Enable`,
                        tags: ['mod', 'option', 'enable'],
                        args: ARGS,
                        content: `/modset {0} "${m.dir}" "${m.name}" "${s.name}" = "${o.name}"`
                    }];
                }
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
        new($"Moodles (V{DEFAULTS_VERSION})",
            [new("Moodles.GetRegisteredMoodlesInfo")],
"""
// Target name
var ARGS = 'self';

function main(moodlesJson) {
     var moodles = JSON.parse(moodlesJson);
     var macros = moodles.map(m => {
        return {
            name: `Apply Moodle [${m.Item3}]`,
            path: `Moodles/${m.Item3}/Apply`,
            tags: ['moodle', 'apply'],
            args: ARGS,
            content: `/moodle apply {0} moodle "${m.Item1}"`
        };
     });
     return JSON.stringify(macros);
}
"""),
        new($"Plugin Collections (V{DEFAULTS_VERSION})", 
            [new(DalamudIpc.PLUGIN_COLLECTION_NAME_LIST)],
"""
function main(collectionNamesJson) {
     var collectionNames = JSON.parse(collectionNamesJson);
     var macros = collectionNames.flatMap(n => {
        return [{
            name: `Enable Plugin Collection [${n}]`,
            path: `Plugins/Collections/${n}/Enable`,
            tags: ['plugin', 'collection', 'enable'],
            content: `/xlenablecollection "${n}"`
        }, {
            name: `Disable Plugin Collection [${n}]`,
            path: `Plugins/Collections/${n}/Disable`,
            tags: ['plugin', 'collection', 'disable'],
            content: `/xldisablecollection "${n}"`
        }];
     });
     return JSON.stringify(macros);
}
"""),
        new($"Overrides (V{DEFAULTS_VERSION})",
            [new(CustomMacrosIpc.LIST), new(PenumbraIpc.MOD_LIST_WITH_SETTINGS)],
"""
// Examples
const TRANSFORMERS = [
    // Assign custom commands for specific custom emotes
    {match: m => m.path.includes('Remote Shock Collar [Mittens]') && m.tags.includes('emote'), mutate: m => m.command = `/shock${['/upset', '/shocked', '/sulk', '/kneel'].indexOf(m.tags.find(t => t.startsWith('/'))) + 1}`},
    {match: m => m.path.includes('Remote Vibrator [Mittens]') && m.tags.includes('emote'), mutate: m => m.command = `/vibrate${['/blush', '/stagger', '/panic', '/grovel', '/pdead'].indexOf(m.tags.find(t => t.startsWith('/'))) + 1}`},

    // Doze Anywhere plugin support
    {match: m => m.tags.includes('emote') && (m.tags.includes('/sit') || m.tags.includes('/doze')), mutate: m => m.content = m.content.replaceAll(new RegExp('(?<!\\| ?)/(sit|doze)(?=\\W|$)', 'gm'), '/$1anywhere')},

    {match: m => m.path.includes('Eorzean-Nightlife-V2') && m.tags.includes('emote'), mutate: (macro, macros, mods) => {
        // Disable other emote groups to avoid internal conflicts in modpack
        const matches = [...macro.content.matchAll(new RegExp('\n/modset .*? "([^"]*?)" =.*?\n', 'mg'))]
        if (matches.length == 1) {
            const match = matches[0];
            const mod = mods.find(m => m.name.includes('Eorzean-Nightlife-V2'));
            const otherEmoteGroupNames = mod.settings.groupSettings.map(s => s.name).filter(name => ['Default', 'Audio-Animations', match[2]].indexOf(name) === -1);
            const disableOtherEmoteGroupsLines = otherEmoteGroupNames.map(name => `/modset {0} "${mod.dir}" "${mod.name}" "${name}" =`);
            macro.content = `${macro.content.slice(0, match.index)}\n${disableOtherEmoteGroupsLines.join("\n")}${macro.content.slice(match.index)}`;
        }

        // Abort instead of /resetposition for emotes "in that position"
        if (['/wave', '/greet', '/clap', '/huh'].some(command => macro.tags.includes(command))) {
            macro.content = macro.content.replace(new RegExp('^/resetposition .*?$', 'gm'), '/ifinthatposition -v -$');
        }
    }},

    {match: m => m.tags.includes('macro'), mutate: (macro, macros) => {
        // Rewrite /nextmacro from "Macro Chain" plugin
        const macroSetName = ['individual', 'shared'].find(n => macro.tags.includes(n));

        const macroIndex = parseInt(macro.tags.find(t => !isNaN(t)));
        const nextMacro = macros.find(m => m.tags.includes(macroSetName) && m.tags.includes(`${macroIndex + 1}`));
        if (nextMacro) {
            macro.content = macro.content.replace(new RegExp('^/nextmacro\s*$', 'gm'), `/quack exec "${nextMacro.path}" <wait.macro>`);
        }
    }}
];

function main(customMacrosJson, modsJson) {
    const customMacros = JSON.parse(customMacrosJson);
    const mods = JSON.parse(modsJson);

    const transformedMacros = customMacros.flatMap(macro => {
        const transformedMacro = {...macro};
        TRANSFORMERS.forEach(transformer => {
            if (transformer.match(macro)) {
                transformer.mutate(transformedMacro, customMacros, mods);
            }
        });

        return isJsonEqual(macro, transformedMacro) ? [] : [transformedMacro];
    });

    return JSON.stringify(transformedMacros);
}

function isJsonEqual(lhs, rhs) {
    return JSON.stringify(lhs) === JSON.stringify(rhs);
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
