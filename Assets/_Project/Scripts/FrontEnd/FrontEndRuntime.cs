using System;
using System.IO;
using UnityEngine;

// Data-only cross-scene context; no toggled player behaviour or persistent gameplay singleton.
public static class FrontEndRuntime
{
    public static RosterService Rosters { get; private set; }
    public static QuickPlayMatch Match { get; set; }
    public static Action<RosterService> SourceCommitRequested;
    public static string SourceCommitMessage;
    public static Action EditorQuitRequested;
    public static RosterService Open(FrontEndSettings settings)
    {
        if(Rosters==null || Rosters.Settings!=settings)
            Rosters=new RosterService(settings,new RosterSaveStore(Path.Combine(Application.persistentDataPath,"rosters.v1.json")));
        return Rosters;
    }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetPlaySession() { Rosters=null;Match=null;SourceCommitMessage=null; }
}
