using BotMessageRouting.Tests.Data;
using BotMessageRouting.Tests.Utils;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Underscore.Bot.MessageRouting;
using Underscore.Bot.MessageRouting.DataStore;
using Underscore.Bot.MessageRouting.DataStore.Azure;
using Underscore.Bot.MessageRouting.DataStore.Local;
using Underscore.Bot.Models;
using Underscore.Bot.Utils;

namespace BotMessageRouting.Tests
{
    [TestClass]
    public class RoutingDataManagerTests
    {
        public static readonly string SettingsKeyAzureTableStorageConnectionString = "AzureTableStorageConnectionString";

        /// <summary>
        /// Testing the AzureTableStorageRoutingDataManager class requires that you have Azure Table Storage
        /// account set up. In addition, the class to test requires the connection string, which is loaded from
        /// a settings file in your documents folder("Documents" in English Windows 10 versions, in your user
        /// folder).
        /// 
        /// The name of the settings file is defined by the value of TestSettings.TestSettingsFilename string
        /// constant ('BotMessageRoutingTestSettings.json').
        ///
        /// The format of the test settings file is JSON.Here's an example:
        ///
        /// {
        ///     "AzureTableStorageConnectionString": "VALUE HERE"
        /// }
        /// 
        /// WARNING!!! THESE TESTS WILL DELETE ALL MESSAGE ROUTING DATA FROM THE AZURE STORAGE ASSOCIATED WITH
        /// THE CONNECTION STRING SO DO NOT USE WITH YOUR PRODUCTION STORAGE!!!
        /// </summary>
        private static readonly string SettingsInstructions =
            "\n Testing the AzureTableStorageRoutingDataManager class requires that you have Azure Table Storage    " +
            "\n account set up. In addition, the class to test requires the connection string, which is loaded from " +
            "\n a settings file in your documents folder (\"Documents\" in English Windows 10 versions, in your user" +
            "\n folder).                                                                                            " +
            "\n                                                                                                     " +
            "\n The name of the settings file is defined by the value of TestSettings.TestSettingsFilename string   " +
           $"\n constant ('{TestSettings.TestSettingsFilename}').                                                   " +
            "\n                                                                                                     " +
            "\n The format of the test settings file is JSON. Here's an example:                                    " +
            "\n                                                                                                     " +
            "\n {                                                                                                   " +
           $"\n     \"{SettingsKeyAzureTableStorageConnectionString}\": \"VALUE HERE\"                              " +
            "\n }                                                                                                   " +
            "\n                                                                                                     " +
            "\n WARNING!!! THESE TESTS WILL DELETE ALL MESSAGE ROUTING DATA FROM THE AZURE STORAGE ASSOCIATED WITH  " +
            "\n THE CONNECTION STRING SO DO NOT USE WITH YOUR PRODUCTION STORAGE!!!                                 " +
            "\n";

        public enum PartyType
        {
            User = 0,
            Bot = 1,
            Aggregation = 2
        }

        private const uint NumberOfPartiesToCreate = 10;
        private IList<IRoutingDataManager> _routingDataManagers;
        private GlobalTimeProvider _globalTimeProvider;
        private static bool _firstInitialization = true;

        [TestInitialize]
        public void Initialize()
        {
            Trace.WriteLine("--- Starting test initialization");

            TestSettings testSettings = new TestSettings();
            string azureTableStorageConnectionString = testSettings[SettingsKeyAzureTableStorageConnectionString];

            _globalTimeProvider = new TestGlobalTimeProvider();

            _routingDataManagers = new List<IRoutingDataManager>()
            {
                new LocalRoutingDataManager(_globalTimeProvider)
            };

            if (!string.IsNullOrEmpty(azureTableStorageConnectionString))
            {
                AzureTableStorageRoutingDataManager azureTableStorageRoutingDataManager =
                    new AzureTableStorageRoutingDataManager(
                        azureTableStorageConnectionString, _globalTimeProvider);

                azureTableStorageRoutingDataManager.DeleteAll();

                _routingDataManagers.Add(azureTableStorageRoutingDataManager);
            }
            else if (_firstInitialization)
            {
                Console.Out.Write(SettingsInstructions);
                Trace.WriteLine($"Cannot test {nameof(AzureTableStorageRoutingDataManager)} class due to missing connectiong string");
                _firstInitialization = false;
            }

            Trace.WriteLine("--- Test initialization done");
        }

        [TestCleanup]
        public void Cleanup()
        {
            Trace.WriteLine("--- Starting test cleanup");

            foreach (IRoutingDataManager routingDataManager in _routingDataManagers)
            {
                routingDataManager.DeleteAll();
            }

            _routingDataManagers.Clear();
            _routingDataManagers = null;

            Trace.WriteLine("--- Test cleanup done");
        }

        /// <summary>
        /// We expect no exceptions, but empty lists.
        /// </summary>
        [TestMethod]
        public void TestGettersWhenNoDataAdded()
        {
            foreach (IRoutingDataManager routingDataManager in _routingDataManagers)
            {
                Trace.WriteLine($"--- Starting test \"{GetCurrentMethodName()}\" for {routingDataManager.GetType().Name}");
                Assert.AreEqual(0, routingDataManager.GetUserParties().Count);
                Assert.AreEqual(0, routingDataManager.GetBotParties().Count);
                Assert.AreEqual(0, routingDataManager.GetAggregationParties().Count);
                Assert.AreEqual(0, routingDataManager.GetConnectedParties().Count);
                Trace.WriteLine($"--- Test \"{GetCurrentMethodName()}\" done for {routingDataManager.GetType().Name}");
            }
        }

        [TestMethod]
        public void AddAndRemoveUserPartiesValidData()
        {
            AddAndRemovePartiesValidData(PartyType.User);
        }

        [TestMethod]
        public void AddAndRemoveAggregationPartiesValidData()
        {
            AddAndRemovePartiesValidData(PartyType.Aggregation);
        }

        [TestMethod]
        public void AddAndRemovePendingRequestValidData()
        {
            IList<Party> userParties =
                PartyTestData.CreateParties(NumberOfPartiesToCreate, true);

            foreach (IRoutingDataManager routingDataManager in _routingDataManagers)
            {
                Trace.WriteLine($"--- Starting test \"{GetCurrentMethodName()}\" for {routingDataManager.GetType().Name}");

                foreach (Party party in userParties)
                {
                    DateTime expectedConnectionRequestTime =
                        (_globalTimeProvider as TestGlobalTimeProvider).NextDateTimeToProvide;

                    MessageRouterResult messageRouterResult =
                        routingDataManager.AddPendingRequest(party, false);

                    Assert.AreEqual(MessageRouterResultType.ConnectionRequested, messageRouterResult.Type);
                    Assert.AreEqual(party, messageRouterResult.ConversationClientParty);

                    IList<Party> pendingRequests = routingDataManager.GetPendingRequests();

                    if (pendingRequests.Count > 0)
                    {
                        Assert.AreEqual(
                            expectedConnectionRequestTime,
                            pendingRequests[pendingRequests.Count - 1].ConnectionRequestTime);
                    }
                    else
                    {
                        Assert.Fail("No pending requests although we just should have successfully added one");
                    }
                }

                Assert.AreEqual((int)NumberOfPartiesToCreate, routingDataManager.GetPendingRequests().Count);

                // The requestor parties should've been added to the list of user parties as well
                Assert.AreEqual((int)NumberOfPartiesToCreate, routingDataManager.GetUserParties().Count);

                // Try to add duplicates
                foreach (Party party in userParties)
                {
                    MessageRouterResult messageRouterResult =
                        routingDataManager.AddPendingRequest(party, false);

                    Assert.AreEqual(MessageRouterResultType.ConnectionAlreadyRequested, messageRouterResult.Type);
                    Assert.AreEqual(party, messageRouterResult.ConversationClientParty);
                }

                // We should still have the original number
                Assert.AreEqual((int)NumberOfPartiesToCreate, routingDataManager.GetPendingRequests().Count);

                IList<Party> userPartiesInStorage = routingDataManager.GetUserParties();
                int numberOfPartiesRemovedFromUserParties = 0;

                for (int i = 0; i < NumberOfPartiesToCreate; ++i)
                {
                    if (i % 2 == 0)
                    {
                        MessageRouterResult messageRouterResult =
                            routingDataManager.RemovePendingRequest(userParties[i]);

                        Assert.AreEqual(MessageRouterResultType.ConnectionRejected, messageRouterResult.Type);
                        Assert.AreEqual(userParties[i], messageRouterResult.ConversationClientParty);
                    }
                    else
                    {
                        // RemoveParty() should also remove the pending request
                        routingDataManager.RemoveParty(userParties[i]);
                        numberOfPartiesRemovedFromUserParties++;
                    }
                }

                Assert.AreEqual(0, routingDataManager.GetPendingRequests().Count);

                Assert.AreEqual(
                    (int)(NumberOfPartiesToCreate - numberOfPartiesRemovedFromUserParties),
                    routingDataManager.GetUserParties().Count);

                Trace.WriteLine($"--- Test \"{GetCurrentMethodName()}\" done for {routingDataManager.GetType().Name}");
            }
        }

        [TestMethod]
        public void ConnectAndDisconnectValidData()
        {
            int numberOfEachClientAndOwnerParties = (int)NumberOfPartiesToCreate / 2;

            IList<Party> clientParties =
                PartyTestData.CreateParties((uint)numberOfEachClientAndOwnerParties, true);
            IList<Party> ownerParties =
                PartyTestData.CreateParties((uint)numberOfEachClientAndOwnerParties, true);

            IList<Party> connectedClientParties = new List<Party>();
            IList<Party> connectedOwnerParties = new List<Party>();

            foreach (IRoutingDataManager routingDataManager in _routingDataManagers)
            {
                Trace.WriteLine($"--- Starting test \"{GetCurrentMethodName()}\" for {routingDataManager.GetType().Name}");

                // 1. No pending requests (should be successful anyhow)

                for (int i = 0; i < numberOfEachClientAndOwnerParties; ++i)
                {
                    DateTime expectedConnectionEstablishedTime =
                        (_globalTimeProvider as TestGlobalTimeProvider).NextDateTimeToProvide;

                    MessageRouterResult messageRouterResult =
                        routingDataManager.ConnectAndClearPendingRequest(ownerParties[i], clientParties[i]);

                    Assert.AreEqual(MessageRouterResultType.Connected, messageRouterResult.Type);

                    Assert.AreNotEqual(null, messageRouterResult.ConversationClientParty);
                    Assert.AreNotEqual(null, messageRouterResult.ConversationOwnerParty);

                    connectedClientParties.Add(messageRouterResult.ConversationClientParty);
                    connectedOwnerParties.Add(messageRouterResult.ConversationOwnerParty);

                    Dictionary<Party, Party> connectedParties = routingDataManager.GetConnectedParties();

                    Assert.AreEqual((i + 1), connectedParties.Count);

                    bool conversationClientPartyWasFound = false;

                    foreach (Party conversationClientParty in connectedParties.Values)
                    {
                        if (conversationClientParty.Equals(messageRouterResult.ConversationClientParty))
                        {
                            Assert.AreEqual(
                                expectedConnectionEstablishedTime,
                                conversationClientParty.ConnectionEstablishedTime);

                            conversationClientPartyWasFound = true;
                            break;
                        }
                    }

                    Assert.AreEqual(true, conversationClientPartyWasFound);
                }

                Assert.AreEqual(numberOfEachClientAndOwnerParties, routingDataManager.GetConnectedParties().Count);

                foreach (Party conversationOwnerParty in connectedOwnerParties)
                {
                    Assert.AreEqual(true, routingDataManager.IsConnected(conversationOwnerParty, ConnectionProfile.Owner));
                }

                foreach (Party conversationClientParty in connectedClientParties)
                {
                    Assert.AreEqual(true, routingDataManager.IsConnected(conversationClientParty, ConnectionProfile.Client));
                }

                foreach (Party conversationOwnerParty in connectedOwnerParties)
                {
                    IList<MessageRouterResult> messageRouterResults =
                        routingDataManager.Disconnect(conversationOwnerParty, ConnectionProfile.Owner);

                    foreach (MessageRouterResult messageRouterResult in messageRouterResults)
                    {
                        Assert.AreEqual(MessageRouterResultType.Disconnected, messageRouterResult.Type);
                    }
                }

                Assert.AreEqual(0, routingDataManager.GetConnectedParties().Count);

                foreach (Party conversationOwnerParty in connectedOwnerParties)
                {
                    Assert.AreEqual(false, routingDataManager.IsConnected(conversationOwnerParty, ConnectionProfile.Owner));
                }

                foreach (Party conversationClientParty in connectedClientParties)
                {
                    Assert.AreEqual(false, routingDataManager.IsConnected(conversationClientParty, ConnectionProfile.Client));
                }

                connectedClientParties.Clear();
                connectedOwnerParties.Clear();

                // 2. With pending requests

                for (int i = 0; i < numberOfEachClientAndOwnerParties; ++i)
                {
                    MessageRouterResult messageRouterResult =
                        routingDataManager.AddPendingRequest(clientParties[i], false);

                    Assert.AreEqual(MessageRouterResultType.ConnectionRequested, messageRouterResult.Type);
                    Assert.AreEqual(1, routingDataManager.GetPendingRequests().Count);

                    messageRouterResult =
                        routingDataManager.ConnectAndClearPendingRequest(ownerParties[i], clientParties[i]);

                    Assert.AreNotEqual(null, messageRouterResult.ConversationClientParty);
                    connectedClientParties.Add(messageRouterResult.ConversationClientParty);

                    Assert.AreEqual(MessageRouterResultType.Connected, messageRouterResult.Type);
                    Assert.AreEqual(0, routingDataManager.GetPendingRequests().Count);
                }

                Assert.AreEqual(numberOfEachClientAndOwnerParties, routingDataManager.GetConnectedParties().Count);

                for (int i = 0; i < connectedClientParties.Count; ++i)
                {
                    Party conversationClientParty = connectedClientParties[i];

                    if (i % 2 == 0)
                    {
                        // Disconnect this time by using the client instead of the owner
                        IList<MessageRouterResult> messageRouterResults =
                            routingDataManager.Disconnect(conversationClientParty, ConnectionProfile.Client);

                        foreach (MessageRouterResult messageRouterResult in messageRouterResults)
                        {
                            Assert.AreEqual(MessageRouterResultType.Disconnected, messageRouterResult.Type);
                        }
                    }
                    else
                    {
                        // Removing the client party should do a disconnect too
                        IList<MessageRouterResult> messageRouterResults =
                            routingDataManager.RemoveParty(conversationClientParty);

                        bool wasDisconnected = false;

                        foreach (MessageRouterResult messageRouterResult in messageRouterResults)
                        {
                            if (messageRouterResult.Type == MessageRouterResultType.Disconnected)
                            {
                                wasDisconnected = true;
                                break;
                            }
                        }

                        Assert.AreEqual(true, wasDisconnected);
                    }
                }

                Assert.AreEqual(0, routingDataManager.GetConnectedParties().Count);

                connectedClientParties.Clear();

                Trace.WriteLine($"--- Test \"{GetCurrentMethodName()}\" done for {routingDataManager.GetType().Name}");
            }
        }

        [TestMethod]
        public void DeleteAll()
        {
            IList<Party> userParties =
                PartyTestData.CreateParties(NumberOfPartiesToCreate, true);
            IList<Party> botParties =
                PartyTestData.CreateParties(NumberOfPartiesToCreate, true);
            IList<Party> aggregationParties =
                PartyTestData.CreateParties(NumberOfPartiesToCreate, false);

            // TODO: Connections

            foreach (IRoutingDataManager routingDataManager in _routingDataManagers)
            {
                Trace.WriteLine($"--- Starting test \"{GetCurrentMethodName()}\" for {routingDataManager.GetType().Name}");

                foreach (Party party in userParties)
                {
                    routingDataManager.AddParty(party, true);
                }

                foreach (Party party in botParties)
                {
                    routingDataManager.AddParty(party, false);
                }

                foreach (Party party in aggregationParties)
                {
                    routingDataManager.AddAggregationParty(party);
                }

                Assert.AreEqual((int)NumberOfPartiesToCreate, routingDataManager.GetUserParties().Count);
                Assert.AreEqual((int)NumberOfPartiesToCreate, routingDataManager.GetBotParties().Count);
                Assert.AreEqual((int)NumberOfPartiesToCreate, routingDataManager.GetAggregationParties().Count);
                // TODO: Connections

                routingDataManager.DeleteAll();

                Assert.AreEqual(0, routingDataManager.GetUserParties().Count);
                Assert.AreEqual(0, routingDataManager.GetBotParties().Count);
                Assert.AreEqual(0, routingDataManager.GetAggregationParties().Count);
                Assert.AreEqual(0, routingDataManager.GetConnectedParties().Count);

                Trace.WriteLine($"--- Test \"{GetCurrentMethodName()}\" done for {routingDataManager.GetType().Name}");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        protected string GetCurrentMethodName()
        {
            StackTrace stackTrace = new StackTrace();
            StackFrame stackFrame = stackTrace?.GetFrame(1);
            return stackFrame?.GetMethod().Name ?? "<unable to resolve method name>";
        }

        private void AddAndRemovePartiesValidData(PartyType partyType)
        {
            IList<Party> parties =
                PartyTestData.CreateParties(
                    NumberOfPartiesToCreate, (partyType != PartyType.Aggregation));

            int numberOPartiesExpected = 0;

            foreach (IRoutingDataManager routingDataManager in _routingDataManagers)
            {
                Trace.WriteLine($"--- Starting test \"{GetCurrentMethodName()}\" for {routingDataManager.GetType().Name}");
                int currentActualPartyCount = 0;

                foreach (Party party in parties)
                {
                    bool wasAdded = false;

                    switch (partyType)
                    {
                        case PartyType.User:
                        case PartyType.Bot:
                            wasAdded = routingDataManager.AddParty(party, (partyType == PartyType.User));

                            currentActualPartyCount = (partyType == PartyType.User)
                                ? routingDataManager.GetUserParties().Count
                                : routingDataManager.GetBotParties().Count;

                            break;
                        case PartyType.Aggregation:
                            wasAdded = routingDataManager.AddAggregationParty(party);
                            currentActualPartyCount = routingDataManager.GetAggregationParties().Count;
                            break;
                    }

                    numberOPartiesExpected++;

                    Assert.AreEqual(true, wasAdded);
                    Assert.AreEqual(numberOPartiesExpected, currentActualPartyCount);

                    if (partyType == PartyType.Aggregation)
                    {
                        Assert.AreEqual(true, routingDataManager.IsAssociatedWithAggregation(party));
                    }
                }

                // Try adding duplicates
                foreach (Party party in parties)
                {
                    bool wasAdded = false;

                    switch (partyType)
                    {
                        case PartyType.User:
                        case PartyType.Bot:
                            wasAdded = routingDataManager.AddParty(party, (partyType == PartyType.User));
                            break;
                        case PartyType.Aggregation:
                            wasAdded = routingDataManager.AddAggregationParty(party);
                            break;
                    }

                    Assert.AreEqual(false, wasAdded);
                }

                // Since duplicates should not be added, the number should stay the same
                Assert.AreEqual(numberOPartiesExpected, currentActualPartyCount);

                numberOPartiesExpected = currentActualPartyCount;

                if (numberOPartiesExpected > 0)
                {
                    foreach (Party party in parties)
                    {
                        switch (partyType)
                        {
                            case PartyType.User:
                            case PartyType.Bot:
                                IList<MessageRouterResult> messageRouterResults =
                                    messageRouterResults = routingDataManager.RemoveParty(party);

                                // The party had no pending requests and no connections so the only
                                // outcome should be a result with OK indicating the party was
                                // removed successfully
                                Assert.AreEqual(1, messageRouterResults.Count);
                                Assert.AreEqual(MessageRouterResultType.OK, messageRouterResults[0].Type);

                                currentActualPartyCount = (partyType == PartyType.User)
                                    ? routingDataManager.GetUserParties().Count
                                    : routingDataManager.GetBotParties().Count;

                                break;
                            case PartyType.Aggregation:
                                bool wasRemoved = routingDataManager.RemoveAggregationParty(party);

                                Assert.AreEqual(true, wasRemoved);

                                currentActualPartyCount = routingDataManager.GetAggregationParties().Count;
                                break;
                        }

                        numberOPartiesExpected--;

                        Assert.AreEqual(numberOPartiesExpected, currentActualPartyCount);
                    }
                }
                else
                {
                    Assert.Fail($"The storage does not contain any parties - cannot hence test removal");
                }

                Trace.WriteLine($"--- Test \"{GetCurrentMethodName()}\" done for {routingDataManager.GetType().Name}");
            }
        }
    }
}
