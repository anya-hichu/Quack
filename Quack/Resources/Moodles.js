// Target name
var ARGS = 'self';

function main(moodlesJson) {
    var moodles = JSON.parse(moodlesJson);
    var macros = moodles.flatMap(m => {
        return [{
            name: `Apply Moodle [${m.Item3}]`,
            path: `Moodles/${escape(m.Item3)}/Apply`,
            tags: ['moodle', 'apply'],
            args: ARGS,
            content: `/moodle apply {0} moodle "${m.Item1}"`
        }, {
            name: `Remove Moodle [${m.Item3}]`,
            path: `Moodles/${escape(m.Item3)}/Remove`,
            tags: ['moodle', 'remove'],
            args: ARGS,
            content: `/moodle remove {0} moodle "${m.Item1}"`
        }];
    });
    return JSON.stringify(macros);
}

function escape(segment) {
    return segment.replaceAll('/', '|');
}
