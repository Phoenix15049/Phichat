namespace Phichat.Application.DTOs.Message;

/// <summary>Where a message lives and who must hear about changes to it.</summary>
public sealed record MessageAudience(Guid SenderId, Guid? ReceiverId, Guid? GroupId, IReadOnlyList<Guid> UserIds);
