using Microsoft.EntityFrameworkCore;
using Phichat.Application.Common.Exceptions;
using Phichat.Application.DTOs.Contact;
using Phichat.Application.Interfaces;
using Phichat.Domain.Entities;
using Phichat.Infrastructure.Data;

namespace Phichat.Infrastructure.Services;

public class ContactService : IContactService
{
    private readonly AppDbContext _db;

    public ContactService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<List<ContactDto>> GetContactsAsync(Guid ownerId)
    {
        return await _db.Contacts
            .Where(c => c.OwnerId == ownerId)
            .Join(_db.Users, c => c.ContactId, u => u.Id, (c, u) => new ContactDto
            {
                ContactId = u.Id,
                Username = u.Username,
                DisplayName = u.DisplayName,
                AvatarUrl = u.AvatarUrl
            })
            .OrderBy(x => x.DisplayName ?? x.Username)
            .ToListAsync();
    }

    public async Task AddContactAsync(Guid ownerId, Guid contactId)
    {
        if (ownerId == contactId)
            throw new BadRequestException("cannot_add_self", "Cannot add yourself.");

        if (!await _db.Users.AnyAsync(u => u.Id == contactId))
            throw new NotFoundException("user_not_found", "User not found.");

        if (await _db.Contacts.AnyAsync(x => x.OwnerId == ownerId && x.ContactId == contactId))
            return;

        _db.Contacts.Add(new Contact { OwnerId = ownerId, ContactId = contactId });

        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            // Added concurrently by another request: the end state is what the caller asked for.
            if (!await _db.Contacts.AsNoTracking().AnyAsync(x => x.OwnerId == ownerId && x.ContactId == contactId))
                throw;
        }
    }

    public async Task RemoveContactAsync(Guid ownerId, Guid contactId)
    {
        var removed = await _db.Contacts
            .Where(x => x.OwnerId == ownerId && x.ContactId == contactId)
            .ExecuteDeleteAsync();

        if (removed == 0)
            throw new NotFoundException("contact_not_found", "Contact not found.");
    }
}
