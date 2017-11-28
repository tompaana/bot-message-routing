using Microsoft.Bot.Connector;
using Microsoft.WindowsAzure.Storage.Table;
using System;
using Underscore.Bot.Models;

namespace Underscore.Bot.Models

{
    public class PartyEntity : TableEntity
    {
        public PartyEntity(string PartitionKey, string RowKey)
        {
            this.PartitionKey = PartitionKey;
            this.RowKey = RowKey;
        }

        public PartyEntity() { }

        public PartyEntity(Party p, PartyType type)
        {
            this.PartitionKey = $"{p.ChannelId}|{type.ToString()}";
            this.RowKey = p.ServiceUrl;
            this.PartyType = type.ToString();
            this.ChannelId = p.ChannelId;
            this.ServiceUrl = p.ServiceUrl;
            if (p.ChannelAccount != null)
            {
                this.PartitionKey = $"{p.ChannelAccount.Id}|{type.ToString()}";
                this.ChannelAccount = p.ChannelAccount;
                this.ChannelAccountID = p.ChannelAccount.Id;
                this.ChannelAccountName = p.ChannelAccount.Name;
            }
            if (p.ConversationAccount != null)
            {
                this.RowKey = p.ConversationAccount.Id;
                this.ConversationAccount = p.ConversationAccount;
                this.ConversationAccountID = p.ConversationAccount.Id;
                this.ConversationAccountName = p.ConversationAccount.Name;
            }
        }

        public string ServiceUrl { get; set; }
        public string ChannelId { get; set; }
        public ChannelAccount ChannelAccount { get; set; }
        public string ChannelAccountID { get; set; }
        public string ChannelAccountName { get; set; }
        public ConversationAccount ConversationAccount { get; set; }
        public string ConversationAccountID { get; set; }
        public string ConversationAccountName { get; set; }
        public string PartyType { get; set; }

        public bool HasMatchingChannelInformation(Party other)
        {
            return (other != null
                && other.ChannelId.Equals(ChannelId)
                && other.ChannelAccount != null
                && ChannelAccount != null
                && other.ChannelAccount.Id.Equals(ChannelAccount.Id));
        }

        public Party ToParty()
        {
            ChannelAccount cha = new ChannelAccount(this.ChannelAccountID, this.ChannelAccountName);
            ConversationAccount ca = new ConversationAccount(null, this.ConversationAccountID, this.ConversationAccountName);
            return new Party(this.ServiceUrl, this.ChannelId, cha, ca);
        }
    }

    public enum PartyType
    {
        Bot,
        User,
        Aggregation,
        PendingRequest, 
        Owner,
        Client
    }
}