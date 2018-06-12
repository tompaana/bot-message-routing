using Microsoft.Bot.Connector.Authentication;
using Microsoft.Bot.Schema;
using Moq;
using Should;
using System;
using System.Threading.Tasks;
using Underscore.Bot.MessageRouting;
using Underscore.Bot.MessageRouting.Logging;
using Underscore.Bot.MessageRouting.DataStore;
using Xunit;

namespace Bot.MessageRouting.Tests
{
    public class MessageRouterTests : TestsFor<MessageRouter>
    {
        private MicrosoftAppCredentials _credentials;

        public override void BeforeTestClassCreation()
        {
            AutoMocker.Inject<MicrosoftAppCredentials>(Credentials);
        }

        #region Useful testhelpers

        private MicrosoftAppCredentials Credentials
        {
            get
            {
                if(_credentials == null)
                    _credentials = new MicrosoftAppCredentials("randomAppId", "randomPassword");

                return _credentials; 
            }
        }

        #endregion  
    }
}
