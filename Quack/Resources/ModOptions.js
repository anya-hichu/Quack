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
