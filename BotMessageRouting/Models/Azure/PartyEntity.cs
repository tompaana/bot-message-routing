using Microsoft.Bot.Schema;
using Microsoft.WindowsAzure.Storage.Table;
using System;
using System.Globalization;

namespace Underscore.Bot.Models.Azure
{
    /// <summary>
    /// Table storage entity that represents a party.
    /// </summary>
    public class PartyEntity : TableEntity
    {
        public string ServiceUrl { get; set; }
        public string ChannelId { get; set; }

        // Channel account
        public string ChannelAccountId { get; set; }
        public string ChannelAccountName { get; set; }

        // Conversation account
        public string ConversationAccountId { get; set; }
        public string ConversationAccountName { get; set; }

        public string PartyEntityType { get; set; }

        public string ConnectionRequestTime { get; set; }
        public string ConnectionEstablishedTime { get; set; }

        protected virtual string DateTimeFormatSpecifier
        {
            get
            {
                return "O";
            }
        }

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
                ChannelAccountId = party.ChannelAccount.Id;
                ChannelAccountName = party.ChannelAccount.Name;
            }

            if (party.ConversationAccount != null)
            {
                ConversationAccountId = party.ConversationAccount.Id;
                ConversationAccountName = party.ConversationAccount.Name;
            }

            ConnectionRequestTime = DateTimeToString(party.ConnectionRequestTime);
            ConnectionEstablishedTime = DateTimeToString(party.ConnectionEstablishedTime);
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

        public virtual Party ToParty()
        {
            ChannelAccount channelAccount = string.IsNullOrEmpty(ChannelAccountId)
                ? null : new ChannelAccount(ChannelAccountId, ChannelAccountName);

            ConversationAccount conversationAccount = new ConversationAccount(null, ConversationAccountId, ConversationAccountName);

            Party party = new Party(ServiceUrl, ChannelId, channelAccount, conversationAccount)
            {
                ConnectionRequestTime = DateTimeFromString(ConnectionRequestTime),
                ConnectionEstablishedTime = DateTimeFromString(ConnectionEstablishedTime)
            };

            return party;
        }

        protected virtual string DateTimeToString(DateTime dateTime)
        {
            return dateTime.ToString(DateTimeFormatSpecifier);
        }

        protected virtual DateTime DateTimeFromString(string dateTimeAsString)
        {
            return DateTime.ParseExact(dateTimeAsString, DateTimeFormatSpecifier, CultureInfo.InvariantCulture);
        }
    }
}