using Microsoft.Bot.Connector;
using Microsoft.WindowsAzure.Storage.Table;
using Underscore.Bot.Models;

namespace Underscore.Bot.MessageRouting.DataStore.Azure
{
    public class PartyTableEntity : TableEntity
    {
        /// <summary>
        /// The party instance.
        /// </summary>
        public Party Party
        {
            get;
            set;
        }

        /// <summary>
        /// Constructor.
        /// </summary>
        public PartyTableEntity()
        {
        }

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="party">A Party instance.</param>
        public PartyTableEntity(Party party)
            : base(party.ChannelId, party.ConversationAccount.Id)
        {
            Party = party;
        }

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="partitionKey">The partition key - should match the channel ID.</param>
        /// <param name="rowKey">The row key - should match the value of the ConversationAccount.Id property.</param>
        protected PartyTableEntity(string partitionKey, string rowKey) : base(partitionKey, rowKey)
        {
            Party = new PartyWithTimestamps(null, partitionKey, null, new ConversationAccount(null, rowKey, null));
        }
    }
}
