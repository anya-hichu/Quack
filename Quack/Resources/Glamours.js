// Chat filters plugin like for example NoSoliciting can help reduce noise when running commands

function main(designsJson) {
    const designs = JSON.parse(designsJson);
    const macros = designs.map(d => {
        return {
            name: `Apply Design [${d.name}]`,
            path: `Glamours/${d.path.replaceAll('\\', '|')}/Apply`,
            tags: ['glamour', 'design', 'apply', d.tags, d.color ? [d.color.toLowerCase()] : []].flat(),
            content: `/glamour apply ${d.id} | <me>; true`
        };
    });
    return JSON.stringify(macros);
}
