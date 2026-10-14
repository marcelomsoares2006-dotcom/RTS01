using System;
using System.Collections;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization;
using System.Security.Permissions;
using UnityEngine;

[Serializable]
public class BinarySerializable : ISerializable
{
#if UNITY_EDITOR
    public static string DATA_DIRECTORY = "Data_Dev";
#else
    public static string DATA_DIRECTORY = "Data";
#endif

    public BinarySerializable() { }

    protected static void Deserialize(
        BinarySerializable instance,
        SerializationInfo info,
        StreamingContext context)
    {
        Type T = instance.GetType();
        foreach (FieldInfo field in T.GetFields())
        {
            if (field.IsStatic) continue;
            if (!BinarySerializableData.CanSerialize(field)) continue;

            Type serializedType = BinarySerializableData.GetSerializedType(field);
            if (serializedType.IsArray)
            {
                Type serializedElementType = serializedType.GetElementType();
                int nItems = info.GetInt32($"{field.Name}__count");
                Array deserializedValue = Array.CreateInstance(serializedElementType, nItems);
                for (int i = 0; i < nItems; i++)
                    deserializedValue.SetValue(info.GetValue($"{field.Name}__{i}", serializedElementType), i);

                object value;
                if (BinarySerializableData.Deserialize(field, deserializedValue, out value))
                    field.SetValue(instance, value);
                else
                    Debug.LogWarning($"Could not serialize field: {field.Name} - getting default values");
            }
            else
            {
                object deserializedValue = info.GetValue(field.Name, field.FieldType);
                object value;
                if (BinarySerializableData.Deserialize(field, deserializedValue, out value))
                    field.SetValue(instance, value);
                else
                    Debug.LogWarning($"Could not serialize field: {field.Name} - getting default values");
            }
        }
    }

    protected BinarySerializable(SerializationInfo info, StreamingContext context)
    {
        Deserialize(this, info, context);
    }

    [SecurityPermission(SecurityAction.Demand, SerializationFormatter = true)]
    public virtual void GetObjectData(SerializationInfo info, StreamingContext context)
    {
        Type T = GetType();
        foreach (FieldInfo field in T.GetFields())
        {
            if (field.IsStatic) continue;
            if (!BinarySerializableData.CanSerialize(field)) continue;

            object value;
            if (BinarySerializableData.Serialize(field, this, out value))
            {
                // Arrays are serialized through the indexed/count contract even
                // when the source value is null. A null array is an empty array
                // for save/load purposes; writing only the field key makes the
                // reader fail before the rest of GameData can be restored.
                Type serializedType = BinarySerializableData.GetSerializedType(field);
                if (serializedType.IsArray)
                {
                    IEnumerator enm = value == null
                        ? Array.Empty<object>().GetEnumerator()
                        : ((IEnumerable)value).GetEnumerator();
                    // write the items and get the count at the same time
                    // (since SerializationInfo uses key-value pairs)
                    int i = 0;
                    while (enm.MoveNext())
                        info.AddValue($"{field.Name}__{i++}", enm.Current);
                    info.AddValue($"{field.Name}__count", i);
                }
                else
                {
                    info.AddValue(field.Name, value);
                }
            }
            else
            {
                Debug.LogWarning($"Could not serialize field: {field.Name} - ignoring");
            }
        }
    }

    public static BinarySerializable Load(string filePath)
    {
        if (!File.Exists(filePath) && !File.Exists(filePath + ".bak"))
        {
            Debug.LogWarning($"File \"{filePath}\" not found! Getting default values.");
            return null;
        }
        return SaveFileStore.Load<BinarySerializable>(filePath);
    }

    public static void Save(string filePath, BinarySerializable instance)
    {
        SaveFileStore.Save(filePath, instance);
    }
}
