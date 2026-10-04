using Phichat.Application.DTOs.Contact;

namespace Phichat.Application.Interfaces;

public interface IContactService
{
    Task<List<ContactDto>> GetContactsAsync(Guid ownerId);

    /// <summary>Adds a contact; adding an existing contact again is a no-op.</summary>
    Task AddContactAsync(Guid ownerId, Guid contactId);

    Task RemoveContactAsync(Guid ownerId, Guid contactId);
}
