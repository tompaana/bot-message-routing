using Microsoft.Bot.Connector;
using System.Collections.Generic;
using Underscore.Bot.Models;

namespace BotMessageRouting.Tests.Data
{
    public class PartyTestData
    {
        public static uint Counter
        {
            get;
            set;
        }

        public static IList<PartyWithTimestamps> CreateParties(
            uint numberOfPartiesToCreate, bool addChannelAccount)
        {
            IList<PartyWithTimestamps> parties = new List<PartyWithTimestamps>();

            for (uint i = 0; i < numberOfPartiesToCreate; ++i)
            {
                Counter++;

                ChannelAccount channelAccount = null;

                if (addChannelAccount)
                {
                    channelAccount = new ChannelAccount(
                        $"channelAccountId{Counter}", $"channelAccountName{Counter}");
                }

                ConversationAccount conversationAccount =
                    new ConversationAccount(
                        false,
                        $"conversationAccountId{Counter}",
                        $"conversationAccountName{Counter}");

                parties.Add(new PartyWithTimestamps(
                    $"serviceUrl{Counter}", $"channelId{Counter}", channelAccount, conversationAccount));
            }

            return parties;
        }
    }
}
