using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Phichat.Application.Interfaces;
using System.Security.Claims;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ContactsController : ControllerBase
{
    private readonly IContactService _contacts;

    public ContactsController(IContactService contacts)
    {
        _contacts = contacts;
    }

    private Guid CurrentUserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpGet]
    public async Task<IActionResult> GetMyContacts()
    {
        return Ok(await _contacts.GetContactsAsync(CurrentUserId));
    }

    [HttpPost("{contactId:guid}")]
    public async Task<IActionResult> AddContact(Guid contactId)
    {
        await _contacts.AddContactAsync(CurrentUserId, contactId);
        return NoContent();
    }

    [HttpDelete("{contactId:guid}")]
    public async Task<IActionResult> RemoveContact(Guid contactId)
    {
        await _contacts.RemoveContactAsync(CurrentUserId, contactId);
        return NoContent();
    }
}
