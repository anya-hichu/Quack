function main(emotesJson) {
    const emotes = JSON.parse(emotesJson);
    const macros = emotes.flatMap(e => {
        var categoryName = `${e.category[0].toUpperCase()}${e.category.slice(1)}`;
        var tags = ['emote', e.category.toLowerCase(), e.command];
        return [{
            name: `Emote [${e.name}]`,
            path: `Emotes/${categoryName}/${e.name}/Execute`,
            tags: tags,
            content: e.command
        }, {
            name: `Emote [${e.name}] [Motion]`,
            path: `Emotes/${categoryName}/${e.name}/Execute [motion]`,
            tags: tags,
            content: `${e.command} motion`
        }];
    });
    return JSON.stringify(macros);
}
