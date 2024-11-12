// Recommended to use ModAutoTagger plugin to define the mod bulk tags for the conflict resolution
// Chat filters plugin like for example NoSoliciting can help reduce noise when running commands

var ARGS = 'Self';

function main(designsJson) {
    const designs = JSON.parse(designsJson);
    const macros = designs.map(d => {
        const contentLines = [
            '/penumbra bulktag disable {0} | all',
            `/glamour apply ${d.id} | <me>; true`
        ];

        return {
            name: `Apply Design [${d.name}]`,
            path: `Glamours/${d.path}/Apply`,
            tags: d.tags.concat(['glamour', 'design', 'apply']),
            args: ARGS,
            content: contentLines.join("\n")
        }
    })
    return JSON.stringify(macros);
}
