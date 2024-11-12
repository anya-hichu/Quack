// Requires Simple Tweak > Command > Equip Job Command to be enabled

const JOBS = [
    'ARC', 'ACN', 'CNJ', 'GLA', 'LNC', 'MRD', 'PGL', 'ROG', 'THM',
    'ALC', 'ARM', 'BSM', 'CUL', 'CRP', 'GSM', 'LTW', 'WVR',
    'BTN', 'FSH', 'MIN',
    'BLM', 'BRD', 'DRG', 'MNK', 'NIN', 'PLD', 'SCH', 'SMN', 'WAR', 'WHM', 'SAM', 'RDM', 'MCH', 'DRK', 'AST', 'GNB', 'DNC', 'SGE', 'RPR', 'VPR', 'PTN', 'BLU'
];

function main() {
    const macros = JOBS.map(j => {
        return {
            name: `Equip Job [${j}]`,
            path: `Jobs/${j}/Equip`,
            tags: ['job', j.toLowerCase(), 'equip'],
            content: `/equipjob ${j}`
        };
    });
    return JSON.stringify(macros);
}
