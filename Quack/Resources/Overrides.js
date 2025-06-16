// Examples
const TRANSFORMERS = [
    // Assign custom commands for specific custom emotes
    {
        match: m => m.path.includes('Remote Shock Collar [Mittens]') && m.tags.includes('option') && m.tags.includes('emote'),
        mutate: m => m.command = `/shock${['/upset', '/shocked', '/sulk', '/kneel'].indexOf(m.tags.find(t => t.startsWith('/'))) + 1}`
    },
    {
        match: m => m.path.includes('Remote Vibrator [Mittens]') && m.tags.includes('option') && m.tags.includes('emote'),
        mutate: m => m.command = `/vibrate${['/blush', '/stagger', '/panic', '/grovel', '/pdead'].indexOf(m.tags.find(t => t.startsWith('/'))) + 1}`
    },

    // Doze anywhere plugin support, press ALT to activate (https://github.com/Warpholomey/DozeAnywhere)
    {
        match: m => m.tags.includes('emote') && (m.tags.includes('/sit') || m.tags.includes('/doze')),
        mutate: m => m.content = m.content.replaceAll(new RegExp('(?<!\\| ?)/(sit|doze)(?=\\W|$)', 'gm'), '/x$1')
    },

    // Support for Nightlife 3+
    {
        match: m => m.path.includes('Nightlife') && m.tags.includes('emote'),
        mutate: (macro, macros, mods) => {
            // Disable other emote groups to avoid internal conflicts in modpack
            const matches = [...macro.content.matchAll(new RegExp('\n/msc (tmp )?(?:set|clear) -c "([^"]*?)" .*? -g "([^"]*?)".*?\n', 'mg'))]
            if (matches.length == 1) {
                const match = matches[0];

                const tmp = match[1];
                const collection = match[2];
                const groupName = match[3];

                const mod = mods.find(m => m.name.includes('Nightlife') && macro.path.includes(m.name));
                const otherGroupNames = mod.settings.groupSettings.map(s => s.name).filter(name => ['Default', 'Audio-Animations', 'Miscellaneous', 'Signboard (Turn 9 On)', 'Breed Expressions (Turn 7 On)', 'Chain Leash VFX (Imperial Salute)', groupName].indexOf(name) === -1);
                const disableOtherGroupsLines = otherGroupNames.map(name => `/msc ${tmp || ''}clear -c "${collection}" -m "${mod.dir}" -n "${mod.name}" -g "${name}" ${(tmp ? '-s Quack -k -4242' : '')}`);
                macro.content = `${macro.content.slice(0, match.index)}\n${disableOtherGroupsLines.join("\n")}${macro.content.slice(match.index)}`;
            }

            // Abort instead of /resetposition for emotes "in that position"
            if (['/wave', '/greet', '/clap', '/huh'].some(command => macro.tags.includes(command))) {
                macro.content = macro.content.replace(new RegExp('^/resetposition .*?$', 'gm'), '/ifinthatposition -v -$');
            }
        }
    },

    // Rewrite /nextmacro from "Macro Chain" plugin
    {
        match: m => m.tags.includes('macro'),
        mutate: (macro, macros) => {
            const macroSetName = ['individual', 'shared'].find(n => macro.tags.includes(n));

            const macroIndex = parseInt(macro.tags.find(t => !isNaN(t)));
            const nextMacro = macros.find(m => m.tags.includes(macroSetName) && m.tags.includes(`${macroIndex + 1}`));
            if (nextMacro) {
                macro.content = macro.content.replace(new RegExp('^/nextmacro\s*$', 'gm'), `/quack exec "${nextMacro.path}" <wait.macro>`);
            }
        }
    }
];

function main(customMacrosJson, modsJson) {
    const customMacros = JSON.parse(customMacrosJson);
    const mods = JSON.parse(modsJson);

    const transformedMacros = customMacros.flatMap(macro => {
        const transformedMacro = { ...macro };
        TRANSFORMERS.forEach(transformer => {
            if (transformer.match(macro)) {
                transformer.mutate(transformedMacro, customMacros, mods);
            }
        });

        return JSON.stringify(macro) === JSON.stringify(transformedMacro) ? [] : [transformedMacro];
    });

    return JSON.stringify(transformedMacros);
}
