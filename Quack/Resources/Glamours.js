// Recommended to use ModAutoTagger plugin to define the mod bulk tags for the conflict resolution
// Chat filters plugin like for example NoSoliciting can help reduce noise when running commands

var ARGS = 'Self';

function main(designsJson) {
    const designs = JSON.parse(designsJson);
    const macros = designs.flatMap(d => {
        const tags = ['glamour', 'design', 'apply', d.tags, d.color ? [d.color.toLowerCase()] : []].flat();
        const applyCommand = `/glamour apply ${d.id} | <me>; true`;
        return [{
            name: `Apply Design [${d.name}]`,
            path: `Glamours/${normalize(d.path)}/Apply`,
            tags: tags,
            args: ARGS,
            content: ['/penumbra bulktag disable {0} | all', applyCommand].join("\n")
        }, {
            name: `Apply Partial Design [${d.name}]`,
            path: `Glamours/${normalize(d.path)}/Apply [partial]`,
            tags: tags.concat(['partial']),
            content: applyCommand
        }]
    });
    return JSON.stringify(macros);
}

function normalize(path) {
    return path.replaceAll('\\', '|');
}
