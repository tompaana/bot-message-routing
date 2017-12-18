using Microsoft.Bot.Connector;
using Microsoft.WindowsAzure.Storage.Table;
using System;

namespace Underscore.Bot.Models

{
    public class PartyWithTimestampsEntity : TableEntity
    {
        public PartyWithTimestampsEntity(string PartitionKey, string RowKey)
        {
            this.PartitionKey = PartitionKey;
            this.RowKey = RowKey;
        }

        public PartyWithTimestampsEntity(PartyWithTimestamps p, PartyType type)
        {
            this.PartitionKey = p.ChannelId;
            this.RowKey = p.ConversationAccount.Id;
            this.PartyType = type;
            this.ChannelAccount = ChannelAccount;
            this.ChannelAccountID = p.ChannelAccount.Id;
            this.ChannelAccountName = p.ChannelAccount.Name;
            this.ConversationAccount = ConversationAccount;
            this.ConversationAccountID = p.ConversationAccount.Id;
            this.ConversationAccountName = p.ConversationAccount.Name;
        }

        public PartyWithTimestampsEntity() { }


        public DateTime ConnectionRequestTime { get; set; }
        public DateTime ConnectionEstablishedTime { get; set; }
        public string ServiceUrl { get; set; }
        public string ChannelId { get; set; }
        public ChannelAccount ChannelAccount { get; set; }
        public string ChannelAccountID { get; set; }
        public string ChannelAccountName { get; set; }
        public ConversationAccount ConversationAccount { get; set; }
        public string ConversationAccountID { get; set; }
        public string ConversationAccountName { get; set; }
        public PartyType PartyType { get; set; }

        public void ResetConnectionRequestTime()
        {
            ConnectionRequestTime = DateTime.MinValue;
        }

        public void ResetConnectionEstablishedTime()
        {
            ConnectionEstablishedTime = DateTime.MinValue;
        }
    }
}   