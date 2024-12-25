function main(rawMacrosJson, localPlayerInfoJson) {
    const rawMacros = JSON.parse(rawMacrosJson);
    const localPlayerInfo = JSON.parse(localPlayerInfoJson);

    const macros = rawMacros.map(m => {
        const name = `Macro [${m.name || 'Blank'}]`;
        return m.set == 0 ? {
            name: name,
            tags: ['individual', 'macro', `${m.index}`],
            path: `Macros/Individual/${localPlayerInfo.name}/${m.index}/${m.name}`,
            content: m.content
        } : {
            name: name,
            tags: ['shared', 'macro', `${m.index}`],
            path: `Macros/Shared/${m.index}/${m.name}`,
            content: m.content
        };
    });
    return JSON.stringify(macros);
}
