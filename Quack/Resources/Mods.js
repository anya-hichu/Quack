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