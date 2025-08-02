function main() {
    const infoJson = IPC.call('Quack.LocalPlayer.GetInfo');
    const info = JSON.parse(infoJson);

    const titlesJson = IPC.call("Honorific.GetCharacterTitleList", info.name, info.homeWorldId);
    const titles = JSON.parse(titlesJson);

    const macros = titles.map(t => {
        const contentLines = [
            `/honorific title disable meta:all`,
            `/honorific title enable ${t.Title}`
        ];
        return {
            name: `Enable Honorific [${t.Title}]`,
            path: `Honorifics/${t.Title.replaceAll('/', '|')}/Enable`,
            tags: ['honorific', 'title', 'enable'],
            content: contentLines.join("\n")
        };
    });

    return JSON.stringify(macros);
}
