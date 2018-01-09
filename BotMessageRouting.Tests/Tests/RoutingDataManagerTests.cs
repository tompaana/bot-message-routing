using BotMessageRouting.Tests.Data;
using BotMessageRouting.Tests.Utils;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using Underscore.Bot.MessageRouting.DataStore;
using Underscore.Bot.MessageRouting.DataStore.Azure;
using Underscore.Bot.MessageRouting.DataStore.Local;
using Underscore.Bot.Models;

namespace BotMessageRouting.Tests
{
    [TestClass]
    public class RoutingDataManagerTests
    {
        public static readonly string SettingsKeyAzureTableStorageConnectionString = "AzureTableStorageConnectionString";

        /// <summary>
        /// Testing the AzureTableStorageRoutingDataManager class requires that you have Azure Table Storage
        /// account set up. In addition, the class to test requires the connection string, which is loaded from
        /// a settings file in your documents folder("Documents" in English Windows versions, in your user
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
        /// </summary>
        private static readonly string SettingsInstructions =
            "\n Testing the AzureTableStorageRoutingDataManager class requires that you have Azure Table Storage    " +
            "\n account set up. In addition, the class to test requires the connection string, which is loaded from " +
            "\n a settings file in your documents folder (\"Documents\" in English Windows versions, in your user   " +
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
            "\n";

        public enum PartyType
        {
            User = 0,
            Bot = 1,
            Aggregation = 2
        }

        private const uint NumberOfPartiesToCreate = 20;
        private IList<IRoutingDataManager> _routingDataManagers;
        private static bool _firstInitialization = true;

        [TestInitialize]
        public void Initialize()
        {
            TestSettings testSettings = new TestSettings();
            string azureTableStorageConnectionString = testSettings[SettingsKeyAzureTableStorageConnectionString];

            _routingDataManagers = new List<IRoutingDataManager>()
            {
                new LocalRoutingDataManager()
            };

            if (!string.IsNullOrEmpty(azureTableStorageConnectionString))
            {
                _routingDataManagers.Add(new AzureTableStorageRoutingDataManager(azureTableStorageConnectionString));
            }
            else if (_firstInitialization)
            {
                Console.Out.Write(SettingsInstructions);
                Trace.WriteLine($"Cannot test {nameof(AzureTableStorageRoutingDataManager)} class due to missing connectiong string");
                _firstInitialization = false;
            }
        }

        [TestCleanup]
        public void Cleanup()
        {
            _routingDataManagers.Clear();
            _routingDataManagers = null;
        }

        [TestMethod]
        public void AddAndRemoveUserParties()
        {
            AddAndRemoveParties(PartyType.User);
        }

        private void AddAndRemoveParties(PartyType partyType)
        {
            IList<PartyWithTimestamps> parties =
                PartyTestData.CreateParties(
                    NumberOfPartiesToCreate, (partyType != PartyType.Aggregation));

            int numberOPartiesExpected = 0;

            foreach (IRoutingDataManager routingDataManager in _routingDataManagers)
            {
                int currentActualPartyCount = 0;

                foreach (PartyWithTimestamps party in parties)
                {
                    switch (partyType)
                    {
                        case PartyType.User:
                        case PartyType.Bot:
                            routingDataManager.AddParty(party, (partyType == PartyType.User));

                            currentActualPartyCount = (partyType == PartyType.User)
                                ? routingDataManager.GetUserParties().Count
                                : routingDataManager.GetBotParties().Count;

                            break;
                        case PartyType.Aggregation:
                            routingDataManager.AddAggregationParty(party);
                            currentActualPartyCount = routingDataManager.GetAggregationParties().Count;
                            break;
                    }

                    numberOPartiesExpected++;

                    Assert.AreEqual(numberOPartiesExpected, currentActualPartyCount);
                }

                numberOPartiesExpected = currentActualPartyCount;

                if (numberOPartiesExpected > 0)
                {
                    foreach (PartyWithTimestamps party in parties)
                    {
                        switch (partyType)
                        {
                            case PartyType.User:
                            case PartyType.Bot:
                                routingDataManager.RemoveParty(party);

                                currentActualPartyCount = (partyType == PartyType.User)
                                    ? routingDataManager.GetUserParties().Count
                                    : routingDataManager.GetBotParties().Count;

                                break;
                            case PartyType.Aggregation:
                                routingDataManager.RemoveAggregationParty(party);
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
            }
        }
    }
}
