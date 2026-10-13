namespace Phichat.Application.DTOs.Message;

/// <summary>One emoji on a message and who reacted with it.</summary>
public class ReactionUsersDto
{
    public string Emoji { get; set; } = "";
    public int Count { get; set; }
    public List<Guid> UserIds { get; set; } = new();
}

/// <summary>
/// All reactions of a message after a change. <see cref="Version"/> increases with time, so a client
/// receiving updates out of order keeps the newest one instead of mixing counts.
/// </summary>
public class ReactionSnapshot
{
    public List<ReactionUsersDto> Reactions { get; set; } = new();
    public long Version { get; set; }
}
