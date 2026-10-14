using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization;
using UnityEngine;

public class BinarySerializableScriptableObject : ScriptableObject
{
    [SerializeField]
    protected List<string> _fieldsToSerialize;
    public List<string> FieldsToSeralize => _fieldsToSerialize;

    public bool SerializesField(string fieldName)
    {
        if (_fieldsToSerialize == null)
            return false;
        return _fieldsToSerialize.Contains(fieldName);
    }

    public void ToggleSerializeField(string fieldName)
    {
        if (_fieldsToSerialize == null)
            _fieldsToSerialize = new List<string>();

        if (SerializesField(fieldName))
            _fieldsToSerialize.Remove(fieldName);
        else
            _fieldsToSerialize.Add(fieldName);
    }

    public void SaveToFile(string fileName = null, bool serializeAll = false)
    {
        string dirPath = Path.Combine(
            Application.persistentDataPath,
            BinarySerializable.DATA_DIRECTORY
        );
        string filePath = Path.Combine(
            dirPath,
            $"{(fileName == null ? name : fileName)}.data");

        string fullDirPath = Path.GetDirectoryName(filePath);
        if (!Directory.Exists(fullDirPath))
            Directory.CreateDirectory(fullDirPath);

        BinarySerializableData data = new BinarySerializableData(
            this, _fieldsToSerialize, serializeAll);
        try
        {
            SaveFileStore.Save(filePath, data.properties);
        }
        catch (Exception e)
        {
            Debug.LogError($"Failed to serialize '{name}'. Reason: " + e.Message);
        }
    }

    public void LoadFromFile(string fileName = null)
    {
        string filePath = Path.Combine(
            Application.persistentDataPath,
            BinarySerializable.DATA_DIRECTORY,
            $"{(fileName == null ? name : fileName)}.data"
        );

        if (!File.Exists(filePath) && !File.Exists(filePath + ".bak"))
        {
            Debug.LogWarning($"File \"{filePath}\" not found! Getting default values.", this);
            return;
        }

        Dictionary<string, object> properties = null;
        try
        {
            properties = SaveFileStore.Load<Dictionary<string, object>>(filePath);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"Failed to deserialize '{name}' - getting default values. Reason: " + e.Message);
        }

        if (properties == null)
            return;

        Type T = GetType();
        FieldInfo field;
        foreach (KeyValuePair<string, object> pair in properties)
        {
            field = T.GetField(pair.Key);
            object deserializedValue = null;
            if (BinarySerializableData.Deserialize(field, pair.Value, out deserializedValue))
                field.SetValue(this, deserializedValue);
        }
    }
}
