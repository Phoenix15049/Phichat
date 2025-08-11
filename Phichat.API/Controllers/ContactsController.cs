using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Phichat.Application.DTOs.Contact;
using Phichat.Infrastructure.Data;
using System.Security.Claims;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ContactsController : ControllerBase
{
    private readonly AppDbContext _db;
    public ContactsController(AppDbContext db) { _db = db; }

    [HttpGet]
    public async Task<IActionResult> GetMyContacts()
    {
        var me = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        var contacts = await _db.Contacts
            .Where(c => c.OwnerId == me)
            .Join(_db.Users, c => c.ContactId, u => u.Id, (c, u) => new ContactDto
            {
                ContactId = u.Id,
                Username = u.Username,
                DisplayName = u.DisplayName,
                AvatarUrl = u.AvatarUrl
            })
            .OrderBy(x => x.DisplayName ?? x.Username)
            .ToListAsync();

        return Ok(contacts);
    }

    [HttpPost("{contactId:guid}")]
    public async Task<IActionResult> AddContact(Guid contactId)
    {
        var me = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        if (me == contactId) return BadRequest("Cannot add yourself.");

        var exists = await _db.Contacts.AnyAsync(x => x.OwnerId == me && x.ContactId == contactId);
        if (exists) return NoContent();

        _db.Contacts.Add(new Phichat.Domain.Entities.Contact { OwnerId = me, ContactId = contactId });
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{contactId:guid}")]
    public async Task<IActionResult> RemoveContact(Guid contactId)
    {
        var me = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var row = await _db.Contacts.FirstOrDefaultAsync(x => x.OwnerId == me && x.ContactId == contactId);
        if (row == null) return NotFound();
        _db.Contacts.Remove(row);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}
