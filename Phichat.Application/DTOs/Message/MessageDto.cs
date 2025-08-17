using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Phichat.Application.DTOs.Message
{
    public class MessageDto
    {
        public Guid Id { get; set; }
        public string Text { get; set; }
        public Guid SenderId { get; set; }
        public DateTime SentAtUtc { get; set; }
        public bool IsRead { get; set; }

        public Guid? ReplyToMessageId { get; set; }
        public string? ReplyPreview { get; set; }

    }
}
