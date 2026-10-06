using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.SignalR;
using Phichat.API.Hubs;
using Phichat.API.Security;
using Phichat.Application.Common.Exceptions;
using Phichat.Application.DTOs.Group;
using Phichat.Application.DTOs.User;
using Phichat.Application.Interfaces;

namespace Phichat.API.Controllers;

/// <summary>
/// Group management. Every change is pushed to the members ("GroupUpdated", plus the service message
/// as "ReceiveMessage"); users who are no longer members get "GroupRemoved".
/// </summary>
[ApiController]
[Route("api/groups")]
[Authorize]
public class GroupsController : ControllerBase
{
    private const long MaxAvatarBytes = 10_000_000;

    private readonly IGroupService _groups;
    private readonly IHubContext<ChatHub> _hub;

    public GroupsController(IGroupService groups, IHubContext<ChatHub> hub)
    {
        _groups = groups;
        _hub = hub;
    }

    private Guid CurrentUserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateGroupRequest request)
    {
        var result = await _groups.CreateAsync(CurrentUserId, request);
        await PublishAsync(result);
        return Ok(await _groups.GetAsync(CurrentUserId, result.GroupId));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id)
    {
        return Ok(await _groups.GetAsync(CurrentUserId, id));
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateGroupRequest request)
    {
        await PublishAsync(await _groups.UpdateAsync(CurrentUserId, id, request));
        return NoContent();
    }

    [HttpPost("{id:guid}/avatar")]
    [Consumes("multipart/form-data")]
    [EnableRateLimiting(RateLimitPolicies.Upload)]
    [RequestFormLimits(MultipartBodyLengthLimit = MaxAvatarBytes)]
    [RequestSizeLimit(MaxAvatarBytes)]
    public async Task<IActionResult> UploadAvatar(Guid id, [FromForm] AvatarUploadRequest model)
    {
        await _groups.EnsureAdminAsync(CurrentUserId, id);

        var file = model.File;
        if (file == null || file.Length == 0)
            throw new BadRequestException("file_required", "No file.");

        // Trust the bytes, not the client's file name or content type.
        var ext = await ImageSignature.DetectExtensionAsync(file)
            ?? throw new BadRequestException("invalid_image", "Only JPEG, PNG, WebP or GIF images are allowed.");

        // wwwroot/uploads/groups/{groupId}/yyyyMMddHHmmssfff.ext
        var dir = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "groups", id.ToString());
        Directory.CreateDirectory(dir);
        var fileName = $"{DateTime.UtcNow:yyyyMMddHHmmssfff}{ext}";
        await using (var stream = new FileStream(Path.Combine(dir, fileName), FileMode.Create))
            await file.CopyToAsync(stream);

        var url = $"/uploads/groups/{id}/{fileName}";
        await PublishAsync(await _groups.SetAvatarAsync(CurrentUserId, id, url));
        return Ok(new { url });
    }

    [HttpDelete("{id:guid}/avatar")]
    public async Task<IActionResult> RemoveAvatar(Guid id)
    {
        await PublishAsync(await _groups.SetAvatarAsync(CurrentUserId, id, null));
        return NoContent();
    }

    [HttpPost("{id:guid}/members")]
    public async Task<IActionResult> AddMembers(Guid id, [FromBody] AddGroupMembersRequest request)
    {
        await PublishAsync(await _groups.AddMembersAsync(CurrentUserId, id, request.UserIds));
        return NoContent();
    }

    [HttpDelete("{id:guid}/members/{userId:guid}")]
    public async Task<IActionResult> RemoveMember(Guid id, Guid userId)
    {
        await PublishAsync(await _groups.RemoveMemberAsync(CurrentUserId, id, userId));
        return NoContent();
    }

    [HttpPut("{id:guid}/members/{userId:guid}/role")]
    public async Task<IActionResult> SetRole(Guid id, Guid userId, [FromBody] SetGroupRoleRequest request)
    {
        await PublishAsync(await _groups.SetRoleAsync(CurrentUserId, id, userId, request.Role));
        return NoContent();
    }

    [HttpPost("{id:guid}/leave")]
    public async Task<IActionResult> Leave(Guid id)
    {
        await PublishAsync(await _groups.LeaveAsync(CurrentUserId, id));
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        await PublishAsync(await _groups.DeleteAsync(CurrentUserId, id));
        return NoContent();
    }

    /// <summary>The members' current identity keys, to encrypt a message for the group.</summary>
    [HttpGet("{id:guid}/keys")]
    public async Task<IActionResult> GetKeys(Guid id)
    {
        return Ok(await _groups.GetMemberKeysAsync(CurrentUserId, id));
    }

    private async Task PublishAsync(GroupChangeResult result)
    {
        var members = ToUserIds(result.MemberIds);

        if (result.SystemMessage is { } message && members.Count > 0)
        {
            await _hub.Clients.Users(members).SendAsync("ReceiveMessage", new
            {
                MessageId = message.Id,
                SenderId = message.SenderId,
                GroupId = message.GroupId,
                SystemEvent = message.SystemEvent,
                EncryptedText = "",
                SentAt = message.SentAt
            });
        }

        if (members.Count > 0)
            await _hub.Clients.Users(members).SendAsync("GroupUpdated", new { groupId = result.GroupId });

        if (result.RemovedUserIds.Count > 0)
            await _hub.Clients.Users(ToUserIds(result.RemovedUserIds)).SendAsync("GroupRemoved", new { groupId = result.GroupId });
    }

    private static List<string> ToUserIds(IEnumerable<Guid> ids) => ids.Select(id => id.ToString()).ToList();
}
