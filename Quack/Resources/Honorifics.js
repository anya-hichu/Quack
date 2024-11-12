// Second IPC argument value (WorldId) can be found as key in %appdata%\xivlauncher\pluginConfigs\Honorific.json

function main(titlesJson) {
    const titles = JSON.parse(titlesJson);

    const disablehonorificsMacro = {
        name: `Disable Honorifics`,
        path: 'Macros/Customs/Disable Honorifics',
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
            path: `Honorifics/${t.Title.replaceAll('/', '|')}/Enable`,
            tags: ['honorific', 'title', 'enable'],
            content: contentLines.join("\n")
        };
    });

    const macros = [disablehonorificsMacro].concat(titleMacros);

    return JSON.stringify(macros);
}
