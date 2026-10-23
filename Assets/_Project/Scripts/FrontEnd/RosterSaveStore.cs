using System;
using System.IO;
using UnityEngine;

// Same-directory replace keeps the previous complete save available on macOS and Windows.
public sealed class RosterSaveStore
{
    public string Path { get; }
    public RosterSaveStore(string path) => Path = path;
    public string Read(bool backup = false) => File.Exists(Path + (backup ? ".bak" : "")) ? File.ReadAllText(Path + (backup ? ".bak" : "")) : null;
    public void Write(RosterSaveData data)
    {
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path));
        string temporary = Path + ".tmp";
        try
        {
            File.WriteAllText(temporary, JsonUtility.ToJson(data, true));
            if (File.Exists(Path)) File.Replace(temporary, Path, Path + ".bak");
            else File.Move(temporary, Path);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}
