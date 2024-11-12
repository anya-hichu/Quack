using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.Linq;

namespace Quack.Utils;

public static class NewtonsoftUtil
{
    public static Dictionary<string, object> CamelCaseDictionnary(Dictionary<string, object> dictionnary)
    {
        return dictionnary.ToDictionary(CamelCaseKey, MaybeCamelCaseValueKeys);
    }

    private static string CamelCaseKey(KeyValuePair<string, object> pair)
    {
        return char.ToLowerInvariant(pair.Key[0]) + pair.Key.Substring(1);
    }

    private static object MaybeCamelCaseValueKeys(KeyValuePair<string, object> pair)
    {
        if (pair.Value is JObject jObject)
        {
            var nestedDict = jObject.ToObject<Dictionary<string, object>>();
            if (nestedDict != null)
            {
                return nestedDict.ToDictionary(CamelCaseKey, MaybeCamelCaseValueKeys);
            }
            else
            {
                return pair.Value;
            }
        }
        else if (pair.Value is JArray jArray)
        {
            var maybeNestedDicts = jArray.ToObject<List<Dictionary<string, object>>>();
            if (maybeNestedDicts != null)
            {
                return maybeNestedDicts.Select(d => d.ToDictionary(CamelCaseKey, MaybeCamelCaseValueKeys));
            }
            else
            {
                return pair.Value;
            }
        }
        else
        {
            return pair.Value;
        }
    }
}
