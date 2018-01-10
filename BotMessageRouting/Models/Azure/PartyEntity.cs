using Microsoft.Bot.Connector;
using Microsoft.WindowsAzure.Storage.Table;
using System;

namespace Underscore.Bot.Models.Azure
{
    public enum PartyEntityType
    {
        Bot,
        User,
        Aggregation,
        PendingRequest,
        Owner,
        Client
    }

    /// <summary>
    /// Table storage entity that represents a party.
    /// </summary>
    public class PartyEntity : TableEntity
    {
        public string ServiceUrl { get; set; }
        public string ChannelId { get; set; }
        public ChannelAccount ChannelAccount { get; set; }
        public string ChannelAccountID { get; set; }
        public string ChannelAccountName { get; set; }
        public ConversationAccount ConversationAccount { get; set; }
        public string ConversationAccountID { get; set; }
        public string ConversationAccountName { get; set; }
        public string PartyEntityType { get; set; }
        public DateTime ConnectionRequestTime { get; set; }
        public DateTime ConnectionEstablishedTime { get; set; }

        public PartyEntity()
        {
        }

        public PartyEntity(string partitionKey, string rowKey)
        {
            PartitionKey = partitionKey;
            RowKey = rowKey;
        }

        public PartyEntity(Party party, PartyEntityType partyEntityType)
        {
            PartitionKey = CreatePartitionKey(party, partyEntityType);
            RowKey = CreateRowKey(party);

            ChannelId = party.ChannelId;
            ServiceUrl = party.ServiceUrl;
            PartyEntityType = partyEntityType.ToString();

            if (party.ChannelAccount != null)
            {
                ChannelAccount = party.ChannelAccount;
                ChannelAccountID = party.ChannelAccount.Id;
                ChannelAccountName = party.ChannelAccount.Name;
            }

            if (party.ConversationAccount != null)
            {
                ConversationAccount = party.ConversationAccount;
                ConversationAccountID = party.ConversationAccount.Id;
                ConversationAccountName = party.ConversationAccount.Name;
            }
        }

        public static string CreatePartitionKey(Party party, PartyEntityType partyEntityType)
        {
            if (party.ChannelAccount != null)
            {
                return $"{party.ChannelAccount.Id}|{partyEntityType.ToString()}";
            }

            return $"{party.ChannelId}|{partyEntityType.ToString()}";
        }

        public static string CreateRowKey(Party party)
        {
            if (party.ConversationAccount != null)
            {
                return party.ConversationAccount.Id;
            }

            return party.ServiceUrl;
        }

        public void ResetConnectionRequestTime()
        {
            ConnectionRequestTime = DateTime.MinValue;
        }

        public void ResetConnectionEstablishedTime()
        {
            ConnectionEstablishedTime = DateTime.MinValue;
        }

        public virtual Party ToParty()
        {
            ChannelAccount channelAccount = new ChannelAccount(ChannelAccountID, ChannelAccountName);
            ConversationAccount conversationAccount = new ConversationAccount(null, ConversationAccountID, ConversationAccountName);

            Party party = new Party(ServiceUrl, ChannelId, channelAccount, conversationAccount)
            {
                ConnectionRequestTime = ConnectionRequestTime,
                ConnectionEstablishedTime = ConnectionEstablishedTime
            };

            return party;
        }
    }
}