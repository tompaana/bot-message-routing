Bot Message Routing (component)
===============================

[![Build status](https://ci.appveyor.com/api/projects/status/ig99aq8273sx2tyh?svg=true)](https://ci.appveyor.com/project/tompaana/bot-message-routing)
[![Nuget status](https://img.shields.io/nuget/v/Underscore.Bot.MessageRouting.svg)](https://www.nuget.org/packages/Underscore.Bot.MessageRouting)

This project is a message routing component for chatbots built with
[Microsoft Bot Framework](https://dev.botframework.com/) C# SDK. It enables routing messages between
users on different channels. In addition, it can be used in advanced customer service scenarios
where the normal routines are handled by a bot, but in case the need arises, the customer can be
connected with a human customer service agent.

**For an example on how to take this code into use, see
[Intermediator Bot Sample](https://github.com/tompaana/intermediator-bot-sample).**

### Possible use cases ###

* Routing messages between users/bots
    * See [Chatbots as Middlemen blog post](https://tompaana.github.io/content/chatbots_as_middlemen.html)
* Customer service scenarios where (in tricky cases) the customer requires a human customer service agent
    * See [Intermediator Bot Sample](https://github.com/tompaana/intermediator-bot-sample)
* Keeping track of users the bot interacts with
* Sending notifications
    * For more information see [this blog post](https://tompaana.github.io/content/remotely_controlled_bots.html) and
      [this sample](https://github.com/tompaana/remote-control-bot-sample)

This is a **.NET Core** project compatible with
[**Bot Framework v4**](https://github.com/Microsoft/botbuilder-dotnet). The .NET Framework based
solution targeting Bot Framework v3.x can be found under releases
[here](https://github.com/tompaana/bot-message-routing/releases/tag/v1.0.2).

If you're looking to build your bot using the **Node.js** SDK instead, here's 
[the Node.js/Typescript message routing project](https://github.com/GeekTrainer/botframework-routing).

## Implementation ##

### Terminology ###

| Term | Description |
| ---- | ----------- |
| Aggregation (channel) | A channel where the chat requests are sent. The users in the aggregation channel can accept the requests. **Applies only if the aggregation channel based approach is used!** |
| Connection | Is created when a request is accepted - the acceptor and the requester form a connection (1:1 chat where the bot relays the messages between the users). |

The [ConversationReference](https://docs.botframework.com/en-us/csharp/builder/sdkreference/d2/d10/class_microsoft_1_1_bot_1_1_connector_1_1_conversation_reference.html)
class, provided by the Bot Framework, is used to define the user/bot identities. The instances of
the class are managed by the
[RoutingDataManager](/BotMessageRouting/MessageRouting/DataStore/RoutingDataManager.cs) class.

### Interfaces ###

#### [IRoutingDataStore](/BotMessageRouting/MessageRouting/DataStore/IRoutingDataStore.cs) ####

An interface for storing the routing data, which includes:

* Users
* Bot instances
* Aggregation channels
* Connection requests
* Connections

An implementation of this interface is passed to the constructor of the
[MessageRouter](/BotMessageRouting/MessageRouting/MessageRouter.cs) class. This solution provides
two implementations of the interface out-of-the-box:
[InMemoryRoutingDataStore](/BotMessageRouting/MessageRouting/DataStore/InMemory/InMemoryRoutingDataStore.cs)
(to be used only for testing) and
[AzureTableRoutingDataStore](/BotMessageRouting/MessageRouting/DataStore/Azure/AzureTableRoutingDataStore.cs).
  
The classes implementing the interface should avoid adding other than storage related sanity checks,
because those are already implemented by the
[RoutingDataManager](/BotMessageRouting/MessageRouting/DataStore/RoutingDataManager.cs).

#### [ILogger](/BotMessageRouting/MessageRouting/Logging/ILogger.cs) ####

An interface used by the MessageRouter class to log events and errors.

### Classes ###

#### MessageRouter class ####

**[MessageRouter](/BotMessageRouting/MessageRouting/MessageRouter.cs)** is the main
class of the project. It manages the routing data (using the `RoutingDataManager` together with the
provided `IRoutingDataStore` implementation) and executes the actual message mediation between the
connected users/bots.

##### Properties #####

* [RoutingDataManager](/BotMessageRouting/MessageRouting/DataStore/RoutingDataManager.cs):
  Provides the main interface for accessing and modifying the routing data (see
  [IRoutingDataStore documentation](#iroutingdatastore) for more information about the routing data
  details).

##### Methods #####

* **`CreateSenderConversationReference`** (static): A utility method for creating a
  `ConversationReference` of the sender in the
  [`Activity`](https://docs.botframework.com/en-us/csharp/builder/sdkreference/dc/d2f/class_microsoft_1_1_bot_1_1_connector_1_1_activity.html)
  instance given as an argument.
* **`CreateRecipientConversationReference`** (static): A utility method for creating a
  `ConversationReference` of the recipient in the `Activity` instance given as an argument. Note
  that the recipient is always expected to be a bot.
* **`SendMessageAsync`**: Sends a message to a specified user/bot.
* **`StoreConversationReferences`**: A convenient method for storing the sender and the recipient in
  the `Activity` instance given as an argument. This method is idempotent; the created instances
  are added only if they are new.
* **`CreateConnectionRequest`**: Creates a new connection request on behalf of the given user/bot.
* **`RejectConnectionRequest`**: Removes (rejects) a pending connection request.
* **`ConnectAsync`**: Establishes a connection between the given users/bots. When successful, this
  method removes the associated connection request automatically.
* **`Disconnect`**: Ends the conversation and severs the connection between the users so that the
  messages are no longer relayed.
* **`RouteMessageIfSenderIsConnectedAsync`**: Relays the message in the `Activity` instance given as 
  an argument, if the sender is connected with a user/bot.

#### Other classes ####

**[RoutingDataManager](/BotMessageRouting/MessageRouting/DataStore/RoutingDataManager.cs)**
contains the main logic for routing data management while leaving the storage specific operation
implementations (add, remove, etc.) for the class implementing the `IRoutingDataStore` interface.
This is the other main class of the solution and can be accessed via the `MessageRouter` class.

**[AzureTableRoutingDataStore](/BotMessageRouting/MessageRouting/DataStore/Azure/AzureTableRoutingDataStore.cs)**
implements the `IRoutingDataStore` interface. The constructor takes the connection string of your
Azure Table Storage.

**[InMemoryRoutingDataStore](/BotMessageRouting/MessageRouting/DataStore/InMemory/InMemoryRoutingDataStore.cs)**
implements the `IRoutingDataStore` interface. Note that this class is meant for testing. **Do not
use this class in production!**

**[AbstractMessageRouterResult](/BotMessageRouting/MessageRouting/Results/AbstractMessageRouterResult.cs)**
is the base class defining the results of the various operations implemented by the `MessageRouter`
and the `RoutingDataManager` classes. The concrete result classes are:

* [ConnectionRequestResult](/BotMessageRouting/MessageRouting/Results/ConnectionRequestResult.cs)
* [ConnectionResult](/BotMessageRouting/MessageRouting/Results/ConnectionResult.cs)
* [MessageRoutingResult](/BotMessageRouting/MessageRouting/Results/MessageRoutingResult.cs)
* [ModifyRoutingDataResult](/BotMessageRouting/MessageRouting/Results/ModifyRoutingDataResult.cs)

For classes not mentioned here, see the documentation in the code files.

## Contributing ##

This is a community project and all contributions are more than welcome!

If you want to contribute, please consider the following:
* Use the **development branch** for the base of your changes
* Remember to update documentation (comments in code)
* Run the tests to ensure your changes do not break existing functionality
    * Having an Azure Storage to test is highly recommended
    * Update/write new tests when needed
* Pull requests should be first merged into the development branch

Please do not hesitate to report ideas, bugs etc. under
[issues](https://github.com/tompaana/bot-message-routing/issues)!

### Acknowledgements ###

Special thanks to the contributors (in alphabetical order):

* [Syed Hassaan Ahmed](https://github.com/syedhassaanahmed)
* [Jorge Cupi](https://github.com/jorgecupi)
* [Michael Dahl](https://github.com/micdah)
* [Jamie D](https://github.com/daltskin)
* [Pedro Dias](https://github.com/digitaldias)
* [Drazen Dodik](https://twitter.com/diggthedrazen)
* [Lucas Humenhuk](https://github.com/lcarli)
* [Lilian Kasem](https://github.com/liliankasem)
* [Edouard Mathon](https://github.com/edouard-mathon)
* [Gary Pretty](https://github.com/garypretty)
* [Jessica Tibaldi](https://github.com/jetiba-ms)

Note that you may not find (all of their) contributions in the change history of this project,
because all of the code including this core functionality used to reside in
[Intermediator Bot Sample](https://github.com/tompaana/intermediator-bot-sample) project.
