using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class AppearanceSelection
{
    public string bodyId, faceId, hairId, facialHairId, skinToneId, hairColorId;
    public BodyArchetype bodyArchetype;
    public PlayerHeight height = PlayerHeight.Ft6In0;
    public float headScale = 1f;
    public string[] clothingIds = Array.Empty<string>();
    public string[] accessoryIds = Array.Empty<string>();
    public int appearanceSeed;

    public static AppearanceSelection From(PlayerAppearanceDefinition value) => new AppearanceSelection {
        bodyId=value.bodyId, faceId=value.faceId, hairId=value.hairId, facialHairId=value.facialHairId,
        skinToneId=value.skinToneId, hairColorId=value.hairColorId, bodyArchetype=value.bodyArchetype,
        height=value.heightProfile.height, headScale=value.headScale, clothingIds=(string[])value.clothingIds.Clone(),
        accessoryIds=(string[])value.accessoryIds.Clone(), appearanceSeed=value.appearanceSeed };

    public void Apply(PlayerAppearanceDefinition target, PlayerAppearanceCatalog catalog, string playerId)
    {
        // Saves contain only stable selections, never Unity instance IDs or packaged asset paths.
        target.id = "player." + playerId;
        target.bodyId=bodyId; target.faceId=faceId; target.hairId=hairId; target.facialHairId=facialHairId;
        target.skinToneId=skinToneId; target.hairColorId=hairColorId; target.bodyArchetype=bodyArchetype;
        target.heightProfile=Array.Find(catalog.heightProfiles, p=>p != null && p.height == height);
        target.headScale=headScale; target.clothingIds=clothingIds ?? Array.Empty<string>();
        target.accessoryIds=accessoryIds ?? Array.Empty<string>(); target.appearanceSeed=appearanceSeed;
    }
}

[Serializable]
public class RosterPlayer
{
    public TeamRoster.PlayerRecord record; // Existing identity and rating fields remain the only authority.
    public AppearanceSelection appearance;
    public RosterPlayer Copy() => JsonUtility.FromJson<RosterPlayer>(JsonUtility.ToJson(this));
}

[Serializable]
public class SavedTeamRoster
{
    public string teamId;
    public List<RosterPlayer> players = new();
    public List<LineupAssignment> active = new();
}

[Serializable]
public class RosterSaveData
{
    public int version = 1;
    public List<SavedTeamRoster> teams = new();
    public RosterSaveData Copy() => JsonUtility.FromJson<RosterSaveData>(JsonUtility.ToJson(this));
}

// Central adapters use the existing AttributeStat order, including Swagger before RouteRunning.
public static class RosterRatings
{
    public static readonly string[] Names = { "Passing", "Speed", "Blocking", "Agility", "Catching", "Run Power",
        "Carrying", "Tackling", "Coverage", "D-Moves", "Swagger", "Route Running" };
    public static int[] Get(TeamRoster.PlayerRecord r) => new[] { r.passing,r.speed,r.blocking,r.agility,r.catching,
        r.runPower,r.carrying,r.tackling,r.coverage,r.dMoves,r.swagger,r.routeRunning };
    public static void Set(ref TeamRoster.PlayerRecord r, int index, int value)
    {
        // Explicit mapping avoids reflection or a second serialized rating array.
        switch(index) {
            case 0:r.passing=value;break; case 1:r.speed=value;break; case 2:r.blocking=value;break;
            case 3:r.agility=value;break; case 4:r.catching=value;break; case 5:r.runPower=value;break;
            case 6:r.carrying=value;break; case 7:r.tackling=value;break; case 8:r.coverage=value;break;
            case 9:r.dMoves=value;break; case 10:r.swagger=value;break; case 11:r.routeRunning=value;break;
            default:throw new ArgumentOutOfRangeException(nameof(index)); }
    }
}
