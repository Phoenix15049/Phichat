namespace Phichat.Domain.Entities
{
    public class ChatKey
    {
        public Guid Id { get; set; }
        public Guid SenderId { get; set; }
        public Guid ReceiverId { get; set; }
        public byte[] EncryptedSymmetricKey { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
