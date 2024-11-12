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
