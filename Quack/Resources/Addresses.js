function main(addressListJson) {
    const addressList = JSON.parse(addressListJson);
    const macros = addressList.map(a => {
        return {
             name: `Travel [${a.name}]`,
             tags: ['address', 'travel'],
             path: `Addresses/${a.path.replaceAll('\\', '|')}/Travel`,
             content: `/li ${a.aliasEnabled ? a.alias : `${a.world}, ${a.city}, W${a.ward}, P${a.plot}`}`
        };
   });
  return JSON.stringify(macros);
}
