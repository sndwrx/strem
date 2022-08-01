﻿using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Strem.Core.Variables;

public class VariableDictionaryConvertor : JsonConverter
{
    public static readonly Type VariableDictionaryType = typeof(Dictionary<VariableEntry, string>);

    public override bool CanConvert(Type objectType) => objectType == VariableDictionaryType;
    public override bool CanRead => true;
    
    public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer)
    {
        var dictionary = value == null ? 
            new Dictionary<VariableEntry, string>() : 
            (Dictionary<VariableEntry, string>)value;

        var jArray = new JArray();
        foreach (var entry in dictionary)
        {
            var jObject = new JObject();
            jObject.Add("name", entry.Key.Name);
            
            if(!string.IsNullOrEmpty(entry.Key.Context))
            { jObject.Add("context", entry.Key.Context); }
            
            jObject.Add("value", entry.Value);
            jArray.Add(jObject);
        }
        jArray.WriteTo(writer);
    }

    public override object? ReadJson(JsonReader reader, Type objectType, object? existingValue, JsonSerializer serializer)
    {
        var dictionary = existingValue == null ? 
            new Dictionary<VariableEntry, string>() : 
            (Dictionary<VariableEntry, string>)existingValue;

        var jArray = JArray.Load(reader);
        foreach (JObject entry in jArray)
        {
            VariableEntry variableEntry;
            if (entry.ContainsKey("context"))
            { variableEntry = new VariableEntry(entry["name"].ToString(), entry["context"].ToString()); }
            else
            { variableEntry = new VariableEntry(entry["name"].ToString()); }
            
            dictionary.Add(variableEntry, entry["value"].ToString());
        }
        return dictionary;
    }
}