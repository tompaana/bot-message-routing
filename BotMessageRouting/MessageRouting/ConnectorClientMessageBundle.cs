using Microsoft.Bot.Connector;
using Microsoft.Bot.Schema;
using System;

namespace Underscore.Bot.MessageRouting
{
    /// <summary>
    /// A utility class for sending messages.
    /// </summary>
    public class ConnectorClientMessageBundle
    {
        public ConnectorClient ConnectorClient
        {
            get;
            set;
        }

        public IMessageActivity MessageActivity
        {
            get;
            set;
        }

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="serviceUrl">The service URL.</param>
        /// <param name="newMessageActivity">The message activity to send.</param>
        public ConnectorClientMessageBundle(string serviceUrl, IMessageActivity messageActivity)
        {
            ConnectorClient = new ConnectorClient(new Uri(serviceUrl));
            MessageActivity = messageActivity;
        }

        /// <summary>
        /// Creates a new message activity based on the given arguments.
        /// </summary>
        /// <param name="conversationReferenceToMessage">The ConversationReference instance to send the message to.</param>
        /// <param name="senderChannelAccount">The channel account of the sender.</param>
        /// <param name="messageText">The message text content.</param>
        /// <returns>A newly created message activity.</returns>
        public static IMessageActivity CreateMessageActivity(
            ConversationReference conversationReferenceToMessage, ChannelAccount senderChannelAccount, string messageText)
        {
            IMessageActivity messageActivity = Activity.CreateMessageActivity();
            messageActivity.Conversation = conversationReferenceToMessage.Conversation;
            messageActivity.Text = messageText;

            if (senderChannelAccount != null)
            {
                messageActivity.From = senderChannelAccount;
            }

            if (conversationReferenceToMessage.User != null)
            {
                messageActivity.Recipient = conversationReferenceToMessage.User;
            }

            return messageActivity;
        }
    }
}
