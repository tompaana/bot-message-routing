using Microsoft.Bot.Schema;
using Microsoft.WindowsAzure.Storage.Table;
using System;
using System.Globalization;

namespace Underscore.Bot.Models.Azure
{
    /// <summary>
    /// Table storage entity that represents a ConversationReference.
    /// </summary>
    public class ConversationReferenceEntity : TableEntity
    {
        public string ServiceUrl { get; set; }
        public string ChannelId { get; set; }

        // Channel account
        public string ChannelAccountId { get; set; }
        public string ChannelAccountName { get; set; }

        // Conversation account
        public string ConversationAccountId { get; set; }
        public string ConversationAccountName { get; set; }

        public string ConversationReferenceEntityType { get; set; }

        public string ConnectionRequestTime { get; set; }
        public string ConnectionEstablishedTime { get; set; }

        protected virtual string DateTimeFormatSpecifier
        {
            get
            {
                return "O";
            }
        }

        public ConversationReferenceEntity()
        {
        }

        public ConversationReferenceEntity(string partitionKey, string rowKey)
        {
            PartitionKey = partitionKey;
            RowKey = rowKey;
        }

        public ConversationReferenceEntity(ConversationReference ConversationReference, ConversationReferenceEntityType ConversationReferenceEntityType)
        {
            PartitionKey = CreatePartitionKey(ConversationReference, ConversationReferenceEntityType);
            RowKey = CreateRowKey(ConversationReference);

            ChannelId = ConversationReference.ChannelId;
            ServiceUrl = ConversationReference.ServiceUrl;
            ConversationReferenceEntityType = ConversationReferenceEntityType.ToString();

            if (ConversationReference.ChannelAccount != null)
            {
                ChannelAccountId = ConversationReference.ChannelAccount.Id;
                ChannelAccountName = ConversationReference.ChannelAccount.Name;
            }

            if (ConversationReference.ConversationAccount != null)
            {
                ConversationAccountId = ConversationReference.ConversationAccount.Id;
                ConversationAccountName = ConversationReference.ConversationAccount.Name;
            }

            ConnectionRequestTime = DateTimeToString(ConversationReference.ConnectionRequestTime);
            ConnectionEstablishedTime = DateTimeToString(ConversationReference.ConnectionEstablishedTime);
        }

        public static string CreatePartitionKey(ConversationReference ConversationReference, ConversationReferenceEntityType ConversationReferenceEntityType)
        {
            if (ConversationReference.ChannelAccount != null)
            {
                return $"{ConversationReference.ChannelAccount.Id}|{ConversationReferenceEntityType.ToString()}";
            }

            return $"{ConversationReference.ChannelId}|{ConversationReferenceEntityType.ToString()}";
        }

        public static string CreateRowKey(ConversationReference ConversationReference)
        {
            if (ConversationReference.ConversationAccount != null)
            {
                return ConversationReference.ConversationAccount.Id;
            }

            return ConversationReference.ServiceUrl;
        }

        public virtual ConversationReference ToConversationReference()
        {
            ChannelAccount channelAccount = string.IsNullOrEmpty(ChannelAccountId)
                ? null : new ChannelAccount(ChannelAccountId, ChannelAccountName);

            ConversationAccount conversationAccount = new ConversationAccount(null, ConversationAccountId, ConversationAccountName);

            ConversationReference ConversationReference = new ConversationReference(ServiceUrl, ChannelId, channelAccount, conversationAccount)
            {
                ConnectionRequestTime = DateTimeFromString(ConnectionRequestTime),
                ConnectionEstablishedTime = DateTimeFromString(ConnectionEstablishedTime)
            };

            return ConversationReference;
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