using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;

public class SaveFileStoreTests
{
    [Serializable]
    private sealed class Payload
    {
        public int value;
        public string label;
    }

    private string _path;

    [SetUp]
    public void SetUp()
    {
        _path = Path.Combine(Application.persistentDataPath, "Data_Dev", "Tests", "SaveFileStoreTests.data");
        Cleanup();
    }

    [TearDown]
    public void TearDown() => Cleanup();

    [Test]
    public void SavesVersionedEnvelopeAndRoundTrips()
    {
        SaveFileStore.Save(_path, new Payload { value = 42, label = "roundtrip" });

        byte[] bytes = File.ReadAllBytes(_path);
        Assert.That(bytes.Length, Is.GreaterThan(16));
        Assert.That(SaveFileStore.Load<Payload>(_path).value, Is.EqualTo(42));
        Assert.That(SaveFileStore.Load<Payload>(_path).label, Is.EqualTo("roundtrip"));
    }

    [Test]
    public void RecoversFromInterruptedReplacementBackup()
    {
        SaveFileStore.Save(_path, new Payload { value = 7, label = "backup" });
        File.Move(_path, _path + ".bak");

        Payload loaded = SaveFileStore.Load<Payload>(_path);
        Assert.That(loaded, Is.Not.Null);
        Assert.That(loaded.value, Is.EqualTo(7));
    }

    private void Cleanup()
    {
        if (File.Exists(_path)) File.Delete(_path);
        if (File.Exists(_path + ".tmp")) File.Delete(_path + ".tmp");
        if (File.Exists(_path + ".bak")) File.Delete(_path + ".bak");
    }
}
