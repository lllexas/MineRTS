using System;
using System.Collections.Generic;
using Newtonsoft.Json;

/// <summary>
/// 稀疏数组转换器：只序列化非 default 值的元素喵~
/// 对于大量元素为 default 的数组，可以显著减小 JSON 体积喵~
/// </summary>
public class SparseArrayConverter<T> : JsonConverter<T[]> where T : struct
{
    /// <summary>
    /// 序列化：将数组转成稀疏格式喵~
    /// 只保存非 default 元素的索引和数据喵~
    /// </summary>
    public override void WriteJson(JsonWriter writer, T[] array, JsonSerializer serializer)
    {
        if (array == null)
        {
            writer.WriteNull();
            return;
        }

        // 收集所有非 default 元素的索引和数据喵~
        var sparseEntries = new List<SparseEntry<T>>();
        for (int i = 0; i < array.Length; i++)
        {
            if (!array[i].Equals(default(T)))
            {
                sparseEntries.Add(new SparseEntry<T>
                {
                    Index = i,
                    Data = array[i]
                });
            }
        }

        // 序列化成对象格式：{ "count": 50, "entries": [...] }
        writer.WriteStartObject();
        writer.WritePropertyName("count");
        writer.WriteValue(array.Length);  // 保存原始数组长度喵~
        writer.WritePropertyName("entries");
        serializer.Serialize(writer, sparseEntries);
        writer.WriteEndObject();
    }

    /// <summary>
    /// 反序列化：从稀疏格式还原数组喵~
    /// </summary>
    public override T[] ReadJson(JsonReader reader, Type objectType, T[] existingValue, bool hasExistingValue, JsonSerializer serializer)
    {
        if (reader.TokenType == JsonToken.Null)
        {
            return null;
        }

        var sparseObject = serializer.Deserialize<SparseObject<T>>(reader);
        
        if (sparseObject == null || sparseObject.Entries == null)
        {
            return new T[0];
        }

        // 创建新数组喵~
        T[] array = new T[sparseObject.Count];
        
        // 填充非 default 元素喵~
        foreach (var entry in sparseObject.Entries)
        {
            if (entry.Index >= 0 && entry.Index < array.Length)
            {
                array[entry.Index] = entry.Data;
            }
        }

        return array;
    }
}

/// <summary>
/// 稀疏条目：存储索引和数据喵~
/// </summary>
public class SparseEntry<T> where T : struct
{
    [JsonProperty("i")]
    public int Index;
    
    [JsonProperty("d")]
    public T Data;
}

/// <summary>
/// 稀疏对象：用于反序列化喵~
/// </summary>
public class SparseObject<T> where T : struct
{
    [JsonProperty("count")]
    public int Count;
    
    [JsonProperty("entries")]
    public List<SparseEntry<T>> Entries;
}
