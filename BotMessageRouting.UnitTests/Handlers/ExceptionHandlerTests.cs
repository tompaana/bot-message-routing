using BotMessageRouting.MessageRouting.Handlers;
using BotMessageRouting.MessageRouting.Logging;
using Moq;
using Should;
using System;
using System.Threading.Tasks;
using Xunit;

namespace BotMessageRouting.UnitTests.Handlers
{
    public class ExceptionHandlerTests : TestsFor<ExceptionHandler>
    {
        [Fact]
        public async Task GetAsync_FunctionReferenceIsNull_LogsWarning()
        {
            // Arrange
            Func<Task<int>> nullFunction = null;

            // Act
            var result = await Instance.GetAsync(nullFunction);

            // Assert
            GetMockFor<ILogger>().Verify(l => l.LogWarning(It.IsAny<string>(), "GetAsync_FunctionReferenceIsNull_LogsWarning"));
        }

        [Fact]
        public async Task GetAsync_FunctionReturnsValue_ReturnsThatValue()
        {
            // Arrange
            Func<Task<int>> goodFunction = () => Task.FromResult<int>(13);

            // Act
            var result = await Instance.GetAsync(goodFunction);

            // Assert
            result.ShouldEqual(13);
        }

        [Fact]
        public async Task GetAsync_FunctionThrowsException_ExceptionLoggedAndDefaultValueReturned()
        {
            // Arrange
            var badException = new Exception("I'm bad");
            Func<Task<int>> badFunction = () => throw badException;

            // Act
            var result = await Instance.GetAsync(badFunction);

            // Assert
            result.ShouldEqual(default(int), "Expected default value for integer type");
            GetMockFor<ILogger>().Verify(l => l.LogException(badException, It.IsAny<string>(), "GetAsync_FunctionThrowsException_ExceptionLoggedAndDefaultValueReturned"));
        }

        [Fact]
        public async Task GetAsync_ThrowsExceptionAndWeDontWantItToReturnDefaultType_ItRethrows()
        {
            // Arrange
            var badException = new Exception("I'm bad");
            Func<Task<int>> badFunction = () => throw badException;

            // Act & Assert
            await Assert.ThrowsAsync<Exception>(() => Instance.GetAsync(badFunction, returnDefaultType: false));
        }

        [Fact]
        public async Task GetAsync_FunctionThrowsExceptionWithCustomHandler_CustomHandlerIsExecuted()
        {
            // Arrange
            bool customHandlerWasRun        = false;
            var badException                = new Exception("I'm bad");
            Func<Task<int>> brokenFunction  = () => throw badException;
            Action<Exception> customHandler = e => customHandlerWasRun = true;

            // Act
            var result = await Instance.GetAsync(brokenFunction, customHandler: customHandler);

            // Assert
            customHandlerWasRun.ShouldBeTrue();
        }

        [Fact]
        public void Get_FunctionReferenceIsNull_LogsWarning()
        {
            // Arrange
            Func<int> nullFunction = null;

            // Act
            var result = Instance.Get(nullFunction);

            // Assert
            GetMockFor<ILogger>().Verify(logger => logger.LogWarning(It.IsAny<string>(), It.IsAny<string>()));
        }

        [Fact]
        public void Get_FunctionReturnsValue_ReturnsThatValue()
        {
            // Arrange
            var value = Guid.NewGuid();
            Func<Guid> function = () => value;

            // Act
            var result = Instance.Get(function);

            // Assert
            result.ShouldEqual(value);
        }

        [Fact]
        public void Get_FunctionThrowsException_ExceptionLoggedAndDefaultValueReturned()
        {
            // Arrange
            var badException         = new Exception("I'm bad");
            Func<int> brokenFunction = () => throw badException;

            // Act
            var result = Instance.Get(brokenFunction);

            // Assert
            result.ShouldEqual(default(int), "Expected default for int, but didn't get it");
            GetMockFor<ILogger>().Verify(l => l.LogException(badException, It.IsAny<string>(), "Get_FunctionThrowsException_ExceptionLoggedAndDefaultValueReturned"));
        }

        [Fact]
        public void Get_FunctionThrowsExceptionWithCustomHandler_CustomHandlerIsExecuted()
        {
            // Arrange
            bool customHandlerWasRun = false;
            var badException = new Exception("I'm bad");
            Func<int> brokenFunction = () => throw badException;
            Action<Exception> customHandler = e => customHandlerWasRun = true;

            // Act
            var result = Instance.Get(brokenFunction, customHandler: customHandler);

            // Assert
            customHandlerWasRun.ShouldBeTrue();
        }

        [Fact]
        public void Get_FunctionThrowsExceptionAndWeDontWantItToReturnDefaultType_ItRethrows()
        {
            // Arrange
            var badException = new Exception("I'm bad");
            Func<int> brokenFunction = () => throw badException;

            // Act & Assert
            Assert.Throws<Exception>(() => Instance.Get(brokenFunction, returnDefaultType: false));
        }

        [Fact]
        public async Task ExecuteAsync_FunctionReferenceIsNull_LogsWarning()
        {
            // Arrange
            Func<Task> badFunction = null;

            // Act
            await Instance.ExecuteAsync(badFunction);

            // Assert
            GetMockFor<ILogger>().Verify(l => l.LogWarning(It.IsAny<string>(), "ExecuteAsync_FunctionReferenceIsNull_LogsWarning"));
        }

        [Fact]
        public async Task ExecuteAsync_FunctionWorks_FunctionIsExecuted()
        {
            // Arrange
            bool flagWasSet = false;
            Func<Task> goodFunction = () => { flagWasSet = true; return Task.CompletedTask; };

            // Act
            await Instance.ExecuteAsync(goodFunction);

            // Assert
            flagWasSet.ShouldBeTrue();            
        }

        [Fact]
        public async Task ExecuteAsync_FunctionThrowsException_ExceptionIsLogged()
        {
            // Arrange
            var badException = new Exception("I'm bad");
            Func<Task> badFunction = () => throw badException;

            // Act
            await Instance.ExecuteAsync(badFunction);

            // Assert
            GetMockFor<ILogger>().Verify(l => l.LogException(badException, It.IsAny<string>(), "ExecuteAsync_FunctionThrowsException_ExceptionIsLogged"));
        }

        [Fact]
        public async Task ExecuteAsync_FunctionThrowsAndCustomHandlerExists_CustomHandlerIsExecuted()
        {
            // Arrange
            bool customHandlerWasRun        = false;
            var badException                = new Exception("I'm bad");
            Func<Task> brokenFunction       = () => throw badException;
            Action<Exception> customHandler = e => customHandlerWasRun = true;

            // Act
            await Instance.ExecuteAsync(brokenFunction, customHandler:customHandler);

            // Assert
            customHandlerWasRun.ShouldBeTrue();
        }
    }
}
