using System.Collections.Generic;

namespace Phichat.Application.DTOs.Message
{
    public class PagedMessagesResponse
    {
        public List<ReceivedMessageResponse> Items { get; set; } = new();
        public bool HasMore { get; set; }
        public string? OldestId { get; set; }
    }
}
