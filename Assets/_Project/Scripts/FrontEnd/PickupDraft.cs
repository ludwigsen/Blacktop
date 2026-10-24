using System;
using System.Collections.Generic;
using System.Linq;

public enum PickupSort { Name, Rating, Role }

// Read-only entries detach every installed saved player, including reserves.
public sealed class PickupPlayer
{
    readonly RosterPlayer player;
    readonly LineupAssignment suggestion;
    public string Id => player.record.id;
    public string SourceTeamId { get; }
    public string SourceTeamName { get; }
    public RosterPlayer Player => player.Copy();
    public LineupAssignment Suggestion => suggestion == null ? null : new LineupAssignment { playerId = Id, offense = suggestion.offense, defense = suggestion.defense };
    // Display only: equal-weight mean of the existing twelve ratings, scaled to 100.
    public float Overall => (float)RosterRatings.Get(player.record).Average() * 5f;
    public string Role => suggestion != null ? suggestion.offense.ToString() : "RESERVE";
    public PickupPlayer(RosterPlayer value, string source, string name, LineupAssignment assignment)
    {
        player = value.Copy(); SourceTeamId = source; SourceTeamName = name;
        suggestion = assignment == null ? null : new LineupAssignment { playerId=assignment.playerId, offense=assignment.offense, defense=assignment.defense };
    }
}

public sealed class PickupDraft
{
    readonly List<PickupPlayer> pool;
    readonly List<string> selected = new();
    public IReadOnlyList<PickupPlayer> Pool => pool.AsReadOnly();
    public IReadOnlyList<string> Selected => selected.AsReadOnly();
    public string PoolError { get; }
    public PickupDraft(RosterService service)
    {
        PoolError = service.Validate(service.Snapshot);
        pool = service.Snapshot.teams.SelectMany(team => team.players.Select(player => new PickupPlayer(player, team.teamId,
            service.Settings.Team(team.teamId).identity.teamName, team.active.Find(a => a.playerId == player.record.id)))).ToList();
        if (pool.Select(p => p.Id).Distinct(StringComparer.OrdinalIgnoreCase).Count() < 14)
            PoolError = "Pickup needs at least 14 unique saved players, including reserves. Add players in PLAYERS.";
    }
    public IEnumerable<PickupPlayer> Browse(string search, string team, PickupSort sort)
    {
        var rows = pool.Where(p => (string.IsNullOrEmpty(team) || p.SourceTeamId == team) &&
            (string.IsNullOrWhiteSpace(search) || p.Player.record.displayName.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0 || p.Player.record.jerseyNumber.ToString() == search));
        return sort == PickupSort.Rating ? rows.OrderByDescending(p => p.Overall).ThenBy(p => p.Id) :
            sort == PickupSort.Role ? rows.OrderBy(p => p.Role).ThenBy(p => p.Player.record.displayName) : rows.OrderBy(p => p.Player.record.displayName).ThenBy(p => p.Id);
    }
    public bool Add(string id, out string error)
    {
        error = PoolError;
        if (error == null && !pool.Any(p => p.Id == id)) error = "Choose a saved player from the pool.";
        if (error == null && selected.Contains(id, StringComparer.OrdinalIgnoreCase)) error = "That player is already on your squad.";
        if (error == null && selected.Count == 7) error = "Your seven slots are full. Remove a player to replace them.";
        if (error != null) return false;
        selected.Add(id); return true;
    }
    public void Remove(string id) => selected.Remove(id);
    public List<LineupAssignment> SuggestHuman() => MatchLineups.Suggest(pool.Where(p => selected.Contains(p.Id)).ToList());
    public static List<PickupPlayer> DraftCpu(IReadOnlyList<PickupPlayer> pool, IEnumerable<string> humans, DifficultyTuning tuning, IMatchRandom random)
    {
        var used = new HashSet<string>(humans, StringComparer.OrdinalIgnoreCase);
        var remaining = pool.Where(p => !used.Contains(p.Id)).ToList(); var result = new List<PickupPlayer>();
        // Complement offensive needs while also ranking each candidate's defensive contribution.
        foreach (TeamMember.RosterSlot slot in Enum.GetValues(typeof(TeamMember.RosterSlot)))
        {
            var ranked = remaining.OrderByDescending(p => {
                var record=p.Player.record;
                // Rank the player's best defensive contribution independently of the offensive vacancy.
                return MatchLineups.OffenseScore(record,slot)*.65f +
                    Enumerable.Range(0,7).Max(defense=>MatchLineups.DefenseScore(record,defense))*.35f;
            }).ThenBy(p => p.Id).ToList();
            int window = Math.Min(ranked.Count, 1 + (int)Math.Ceiling((1f - tuning.DraftQuality) * 3f));
            if (window == 0) return result;
            var choice = ranked[random.Next(window)]; result.Add(choice); remaining.Remove(choice);
        }
        return result;
    }
}

// Same GUID/offense/defense contract as saved teams; suggestions never change sources.
public static class MatchLineups
{
    public static string Validate(SavedTeamRoster team)
    {
        if (team?.players == null || team.active == null || team.players.Count < 7 || team.active.Count != 7)
            return "Each squad needs exactly seven active players and all offense/defense slots.";
        var members = new HashSet<string>(team.players.Select(p => p.record.id), StringComparer.OrdinalIgnoreCase);
        if (members.Count != team.players.Count || members.Any(id => !Guid.TryParse(id, out _))) return "Player GUIDs must be valid and unique.";
        if (team.active.Any(a => a == null || !members.Contains(a.playerId) || (int)a.offense < 0 || (int)a.offense > 6 || a.defense < 0 || a.defense > 6) ||
            team.active.Select(a => a.playerId).Distinct(StringComparer.OrdinalIgnoreCase).Count() != 7 ||
            team.active.Select(a => a.offense).Distinct().Count() != 7 || team.active.Select(a => a.defense).Distinct().Count() != 7)
            return "Assign seven unique players to all seven offense slots and all seven independent defense slots.";
        return null;
    }
    public static float OffenseScore(TeamRoster.PlayerRecord p, TeamMember.RosterSlot slot) => slot switch
    {
        TeamMember.RosterSlot.QB => p.passing * 3 + p.agility + p.carrying,
        TeamMember.RosterSlot.RB => p.speed + p.agility + p.runPower + p.carrying * 2,
        TeamMember.RosterSlot.OL1 or TeamMember.RosterSlot.OL2 => p.blocking * 3 + p.runPower + p.tackling,
        _ => p.catching * 2 + p.speed + p.routeRunning + p.agility
    };
    public static float DefenseScore(TeamRoster.PlayerRecord p, int slot) => slot < 2 ? p.dMoves * 2 + p.tackling * 2 + p.speed :
        slot < 4 ? p.tackling * 2 + p.speed + p.coverage + p.dMoves : p.coverage * 2 + p.speed + p.agility + p.tackling;
    public static List<LineupAssignment> Suggest(IReadOnlyList<PickupPlayer> players)
    {
        var result = players.Select(p => new LineupAssignment { playerId = p.Id, offense = (TeamMember.RosterSlot)(-1), defense = -1 }).ToList();
        var usedOffense = new HashSet<TeamMember.RosterSlot>(); var usedDefense = new HashSet<int>();
        foreach (var player in players)
        {
            var hint = player.Suggestion; var target = result.Find(a => a.playerId == player.Id);
            if (hint != null && usedOffense.Add(hint.offense)) target.offense = hint.offense;
            if (hint != null && usedDefense.Add(hint.defense)) target.defense = hint.defense;
        }
        for (int slot = 0; slot < 7; slot++)
        {
            if (!usedOffense.Contains((TeamMember.RosterSlot)slot))
            {
                var best = result.Where(a => (int)a.offense < 0).OrderByDescending(a => OffenseScore(players.First(p => p.Id == a.playerId).Player.record, (TeamMember.RosterSlot)slot)).First();
                best.offense = (TeamMember.RosterSlot)slot;
            }
            if (!usedDefense.Contains(slot))
            {
                var best = result.Where(a => a.defense < 0).OrderByDescending(a => DefenseScore(players.First(p => p.Id == a.playerId).Player.record, slot)).First();
                best.defense = slot;
            }
        }
        return result;
    }
    public static void Swap(List<LineupAssignment> lineup, string id, int slot, bool defense)
    {
        var target = lineup.Single(a => a.playerId == id);
        var other = lineup.Single(a => defense ? a.defense == slot : (int)a.offense == slot);
        if (defense) { other.defense = target.defense; target.defense = slot; }
        else { other.offense = target.offense; target.offense = (TeamMember.RosterSlot)slot; }
    }
}
