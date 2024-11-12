function main(collectionNamesJson) {
     var collectionNames = JSON.parse(collectionNamesJson);
     var macros = collectionNames.flatMap(n => {
        return [{
            name: `Enable Plugin Collection [${n}]`,
            path: `Plugins/Collections/${n}/Enable`,
            tags: ['plugin', 'collection', 'enable'],
            content: `/xlenablecollection "${n}"`
        }, {
            name: `Disable Plugin Collection [${n}]`,
            path: `Plugins/Collections/${n}/Disable`,
            tags: ['plugin', 'collection', 'disable'],
            content: `/xldisablecollection "${n}"`
        }];
     });
     return JSON.stringify(macros);
}
