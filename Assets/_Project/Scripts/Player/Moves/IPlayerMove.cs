// Contract for any committed move (Juke, Hurdle, future moves). State machine only
// talks to this interface — never touches a move's internals directly. Adding a new
// move later means implementing this interface, not editing a switch statement.
public interface IPlayerMove
{
    bool CanTrigger(PlayerContext ctx, PlayerState currentState);
    void Enter(PlayerContext ctx);
    void Tick(PlayerContext ctx, float deltaTime);
    bool IsComplete { get; }
    void Exit(PlayerContext ctx);
}