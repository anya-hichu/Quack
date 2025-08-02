// Requires "DeterministicPose" and "ModSettingCommands" plugins to be installed
// Chat filters plugin like for example NoSoliciting can help reduce noise when running commands

const COLLECTION = 'Self';
const PRIORITY = 9999;
const KEY = -4242;

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

    // Exclude "hidden mods" with starting dot
    const customEmoteMacros = mods.filter(m => !m.path.startsWith('.') && !m.path.includes('/.')).flatMap(mod => {
        const tags = ['mod', 'emote'].concat(mod.favorite ? ['favorite'] : []);

        const modGamePaths = Object.keys(mod.settings.files || {});
        const modCommandsWithPoseIndex = lookupCommandsWithPoseIndex(emotes, modGamePaths);

        const modMacros = modCommandsWithPoseIndex.map(([command, poseIndex]) => {
            const emoteCommands = buildCommands(command, poseIndex);
            const contentLines = [
                `${RESET_POSITION_MACRO.command} <wait.macro>`,
                `/msc tmp assert -c "${escape(COLLECTION)}" -e -p ${PRIORITY} -m "${escape(mod.dir)}" -n "${escape(mod.name)}" -k ${KEY} -sc /macrocancel -sc ${emoteCommands.map(escape).map(escapeCommand).join(' -sc ')}`,
                `/msc tmp revert -c "${escape(COLLECTION)}" -k ${KEY}`,
                `/msc tmp set -c "${escape(COLLECTION)}" -e -p ${PRIORITY} -m "${escape(mod.dir)}" -n "${escape(mod.name)}" -s Quack -k ${KEY}`,
                '/penumbra redraw <me> <wait.2>'
            ].concat(emoteCommands);
            const commandPath = buildCommandPath(command, poseIndex);
            return {
                name: `Custom Emote [${mod.name}] [${commandPath}] [Temporary]`,
                path: `Mods/${normalize(mod.path)}/Temporary/Emotes${commandPath}`,
                tags: tags.concat(['temporary', command]),
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
                        `/msc tmp assert -c "${escape(COLLECTION)}" -m "${escape(mod.dir)}" -n "${escape(mod.name)}" -e -p ${PRIORITY} -g "${escape(setting.name)}" -o "${escape(option.name)}" -k ${KEY} -sc /macrocancel -sc ${emoteCommands.map(escape).map(escapeCommand).join(' -sc ')}`,
                        `/msc tmp revert -c "${escape(COLLECTION)}" -k ${KEY}`,
                        `/msc tmp set -c "${escape(COLLECTION)}" -m "${escape(mod.dir)}" -n "${escape(mod.name)}" -e -p ${PRIORITY} -g "${escape(setting.name)}" -o "${escape(option.name)}" -s Quack -k ${KEY}`,
                        '/penumbra redraw <me> <wait.1>'
                    ].concat(emoteCommands);
                    const commandPath = buildCommandPath(command, poseIndex); 
                    return {
                        name: `Custom Emote [${option.name}] [${commandPath}] [Temporary]`,
                        path: `Mods/${normalize(mod.path)}/Temporary/Settings/${escapePath(setting.name)}/Options/${escapePath(option.name)}/Emotes${commandPath}`,
                        tags: tags.concat(['option', 'temporary', command]),
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

function escape(value) {
    return value.replaceAll('"', '\\"');
}

function escapePath(segment) {
    return segment.replaceAll('/', '|');
}

function escapeCommand(command) {
    return `"${command.replaceAll('[', '[[').replaceAll(']', ']]').replaceAll('<', '[').replaceAll('>', ']')}"`
}

function normalize(path) {
    return path.replaceAll('\\', '|');
}
