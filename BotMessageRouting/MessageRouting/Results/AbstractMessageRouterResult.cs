using Microsoft.Bot.Schema;
using System;
using System.Text;

namespace Underscore.Bot.MessageRouting.Results
{
    [Serializable]
    public abstract class AbstractMessageRouterResult
    {
        public string ErrorMessage
        {
            get;
            set;
        }

        public AbstractMessageRouterResult()
        {
            ErrorMessage = string.Empty;
        }

        public abstract string ToJson();

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
    }
}
