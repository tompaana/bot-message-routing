using BotMessageRouting.MessageRouting.Logging;
using Microsoft.Bot.Connector.Authentication;
using Microsoft.Bot.Schema;
using Moq;
using Should;
using System;
using System.Threading.Tasks;
using Underscore.Bot.MessageRouting;
using Underscore.Bot.MessageRouting.DataStore;
using Xunit;

namespace BotMessageRouting.UnitTests
{
    public class MessageRouterTests : TestsFor<MessageRouter>
    {
        private MicrosoftAppCredentials _credentials;

        public override void BeforeTestClassCreation()
        {
            AutoMocker.Inject<MicrosoftAppCredentials>(Credentials);
        }

        [Fact]
        public void Constructor_WhenLoggerIsNotDefined_UsesDefaultLogger()
        {
            // Arrange
            var routingDataStore = GetMockFor<IRoutingDataStore>().Object;
            ILogger nullLogger   = null;

            // Act
            var testInstance = new MessageRouter(routingDataStore, Credentials, null, nullLogger);

            // Assert
            testInstance.Logger.ShouldBeType(typeof(DebugLogger));
        }

        [Fact]
        public async Task HandleActivityAsync_WhenCalled_CallIsLogged()
        {
            // Arrange
            var activity = new Mock<IMessageActivity>().Object;

            // Act
            await Assert.ThrowsAsync<ArgumentNullException>(async() => {
                await Instance.HandleActivityAsync(activity,
                    tryToRequestConnectionIfNotConnected: false,
                    rejectConnectionRequestIfNoAggregationChannel: true,
                    addSenderNameToMessage: false);
            });

            // Assert            
            GetMockFor<ILogger>().Verify(l => l.Enter("HandleActivityAsync"), Times.Once());
        }

        [Fact]
        public async Task HandleActivityAsync_ActivityIsNull_ThrowsArugmentNullException()
        {
            // Arrange
            IMessageActivity nullActivity = null;

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() => 
                Instance.HandleActivityAsync(nullActivity,
                tryToRequestConnectionIfNotConnected: false,
                rejectConnectionRequestIfNoAggregationChannel: false)
            );
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
