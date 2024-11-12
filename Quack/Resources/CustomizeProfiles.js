// Target name
var ARGS = 'self';

function main(profilesJson) {
    const profiles = JSON.parse(profilesJson);
    const macros = profiles.flatMap(p => {
        return [{
            name: `Enable Profile [${p.Item2}]`,
            path: `Customizations/${p.Item2}/Enable`,
            tags: ['customize', 'profile', 'enable'],
            args: ARGS,
            content: `/customize profile enable {0},${p.Item2}`
        },{
            name: `Disable Profile [${p.Item2}]`,
            path: `Customizations/${p.Item2}/Disable`,
            tags: ['customize', 'profile', 'disable'],
            args: ARGS,
            content: `/customize profile disable {0},${p.Item2}`
        }];
    });
    return JSON.stringify(macros);
}
