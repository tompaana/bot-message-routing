using Microsoft.Bot.Schema;
using System.Collections.Generic;
using System.Text;

namespace Underscore.Bot.MessageRouting
{
    public enum MessageRouterResultType
    {
        NoActionTaken, // No action taken - The result handler should ignore results with this type
        OK, // Action taken, but the result handler should ignore results with this type
        ConnectionRequested,
        ConnectionAlreadyRequested,
        ConnectionRejected,
        Connected,
        Disconnected,
        NoAgentsAvailable,
        NoAggregationChannel,
        FailedToForwardMessage,
        Error // Generic error including e.g. null arguments
    }

    /// <summary>
    /// Represents a result of more complex operations executed by MessageRouter (when
    /// boolean just isn't enough).
    /// 
    /// Note that - as this class serves different kind of operations with different kind of
    /// outcomes - some of the properties can be null. The type of the result defines which
    /// properties are meaningful.
    /// </summary>
    public class MessageRouterResult
    {
        public MessageRouterResultType Type
        {
            get;
            set;
        }

        /// <summary>
        /// Activity instance associated with the result.
        /// </summary>        
        public IActivity Activity
        {
            get;
            set;
        }

        /// <summary>
        /// A valid ConversationResourceResponse of the newly created direct conversation
        /// (between the bot [who will relay messages] and the conversation owner),
        /// if the connection was added and a conversation created successfully
        /// (MessageRouterResultType is Connected).
        /// </summary>
        public ConversationResourceResponse ConversationResourceResponse
        {
            get;
            set;
        }

        /// <summary>
        /// The ConversationReference instance associated with this result.
        /// </summary>
        public IList<ConversationReference> ConversationReferences
        {
            get;
            set;
        }

        public string ErrorMessage
        {
            get;
            set;
        }

        /// <summary>
        /// Constructor.
        /// </summary>
        public MessageRouterResult()
        {
            Type = MessageRouterResultType.NoActionTaken;
            ConversationReferences = new List<ConversationReference>();
            ErrorMessage = string.Empty;
        }

        public static string ChannelAccountToString(ChannelAccount channelAccount)
        {
            StringBuilder stringBuilder = new StringBuilder();

            if (channelAccount == null)
            {
                stringBuilder.Append("null");
            }
            else
            {
                stringBuilder.Append(channelAccount.Id);
                stringBuilder.Append(", ");

                if (string.IsNullOrEmpty(channelAccount.Name))
                {
                    stringBuilder.Append("(no name)");
                }
                else
                {
                    stringBuilder.Append(channelAccount.Name);
                }
            }

            return stringBuilder.ToString();
        }

        public static string ConversationReferenceToString(ConversationReference conversationReference)
        {
            StringBuilder stringBuilder = new StringBuilder();

            if (conversationReference == null)
            {
                stringBuilder.Append("null");
            }
            else
            {
                stringBuilder.Append("Activity ID: ");
                stringBuilder.Append(conversationReference.ActivityId);
                stringBuilder.Append("; Channel ID: ");
                stringBuilder.Append(conversationReference.ChannelId);
                stringBuilder.Append("; Service URL: ");
                stringBuilder.Append(conversationReference.ServiceUrl);
                stringBuilder.Append("; ");

                if (conversationReference.Conversation == null)
                {
                    stringBuilder.Append("(no conversation account)");
                }
                else
                {
                    stringBuilder.Append("Conversation account: ");
                    stringBuilder.Append(conversationReference.Conversation.Id);
                    stringBuilder.Append(", ");

                    if (string.IsNullOrEmpty(conversationReference.Conversation.Name))
                    {
                        stringBuilder.Append("(no name)");
                    }
                    else
                    {
                        stringBuilder.Append(conversationReference.Conversation.Name);
                    }
                }

                stringBuilder.Append("; ");

                ChannelAccount channelAccount = null;

                if (conversationReference.Bot != null)
                {
                    stringBuilder.Append("Bot: ");
                    channelAccount = conversationReference.Bot;
                }
                else if (conversationReference.User != null)
                {
                    stringBuilder.Append("User: ");
                    channelAccount = conversationReference.User;
                }

                if (channelAccount == null)
                {
                    stringBuilder.Append("(no channel account)");
                }
                else
                {
                    stringBuilder.Append(ChannelAccountToString(channelAccount));
                }
            }

            return stringBuilder.ToString();
        }

        public override string ToString()
        {
            StringBuilder stringBuilder = new StringBuilder();
            stringBuilder.Append(Type);
            stringBuilder.Append("; ");
            
            if (ConversationReferences.Count > 0)
            {
                stringBuilder.Append("Conversation references: [{ ");

                for (int i = 0; i < ConversationReferences.Count; ++i)
                {
                    stringBuilder.Append(ConversationReferenceToString(ConversationReferences[i]));
                    stringBuilder.Append(" }");

                    if (i < ConversationReferences.Count - 1)
                    {
                        stringBuilder.Append(", {");
                    }
                    else
                    {
                        stringBuilder.Append("]");
                    }
                }
            }

            if (!string.IsNullOrEmpty(ErrorMessage))
            {
                stringBuilder.Append("; Error message: \"");
                stringBuilder.Append(ErrorMessage);
                stringBuilder.Append("\"");
            }

            return stringBuilder.ToString();
        }
    }
}
