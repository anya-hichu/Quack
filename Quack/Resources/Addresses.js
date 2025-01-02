// Requires Lifestream plugin

function main(addressListJson) {
    const addressList = JSON.parse(addressListJson);
    const macros = addressList.map(address => {
        const location = serialize(address);
        return {
            name: `Travel [${address.name || location}]`,
            tags: ['address', 'travel'],
            path: `Addresses/${(address.name ? address.path : address.path + location).replaceAll('\\', '|')}/Travel`,
            content: `/li ${location}`
        };
    });
    return JSON.stringify(macros);
}

function serialize(address) {
    if (address.aliasEnabled) {
        return `${address.alias}`;
    } else if (address.propertyType == "House") {
        return `${address.world}, ${address.residentialDistrict}, W${address.ward}, P${address.plot}`;
    } else if (address.propertyType == "Apartment") {
        return `${address.world}, ${address.residentialDistrict}, W${address.ward}${address.apartmentSubdivision ? ' Subdivision' : ''}, A${address.apartment}`;
    } else {
        throw `Unsupported property type: ${address.propertyType}`;
    }
}
