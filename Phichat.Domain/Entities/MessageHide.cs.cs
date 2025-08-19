using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Phichat.Domain.Entities
{
    public class MessageHide
    {
        public Guid UserId { get; set; }
        public Guid MessageId { get; set; }
        public DateTime CreatedAt { get; set; }
    }

}
