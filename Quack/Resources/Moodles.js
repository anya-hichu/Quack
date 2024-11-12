// Target name
var ARGS = 'self';

function main(moodlesJson) {
     var moodles = JSON.parse(moodlesJson);
     var macros = moodles.map(m => {
        return {
            name: `Apply Moodle [${m.Item3}]`,
            path: `Moodles/${m.Item3}/Apply`,
            tags: ['moodle', 'apply'],
            args: ARGS,
            content: `/moodle apply {0} moodle "${m.Item1}"`
        };
     });
     return JSON.stringify(macros);
}
