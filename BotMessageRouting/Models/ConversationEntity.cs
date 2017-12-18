using Microsoft.WindowsAzure.Storage.Table;

namespace Underscore.Bot.Models
{
    public class ConversationEntity : TableEntity
    {
        public ConversationEntity(string PartitionKey, string RowKey)
        {
            this.PartitionKey = PartitionKey;
            this.RowKey = RowKey;
        }

        public ConversationEntity() { }

        public string Owner { get; set; }

        public string Client { get; set; }
    }
}
