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
    * See [Chatbots as Middlemen blog post](http://tomipaananen.azurewebsites.net/?p=1851)
* Customer service scenarios where (in tricky cases) the customer requires a human customer service agent
    * See [Intermediator Bot Sample](https://github.com/tompaana/intermediator-bot-sample)
* Keeping track of users the bot interacts with
* Sending notifications
    * For more information see [this blog post](http://tomipaananen.azurewebsites.net/?p=2231) and
      [this sample](https://github.com/tompaana/remote-control-bot-sample)

This is a C# solution, but don't worry if you prefer the **Node.js** SDK; in that case check out
[this sample](https://github.com/palindromed/Bot-HandOff)!

## Implementation ##

### Terminology ###

| Term | Description |
| ---- | ----------- |
| Aggregation (channel) | **Only applies if aggregation approach is used!** A channel where the chat requests are sent. The users in the aggregation channel can accept the requests. |
| Connection | Is created when a request is accepted - the acceptor and the one accepted form a connection (1:1 chat where the bot relays the messages between the users). |
| Party | A user/bot in a specific conversation or can represent a channel (e.g. specific conversation channel in Slack) itself. |
| Conversation client | A reqular user e.g. a customer. |
| Conversation owner | E.g. a customer service **agent**. |

### Interfaces ###

#### [IRoutingDataManager](/BotMessageRouting/MessageRouting/DataStore/IRoutingDataManager.cs) ####

An interface for managing the parties (users/bot), aggregation channel details, the list of
connected parties and pending requests.

### Classes ###

#### MessageRouterManager class ####

**[MessageRouterManager](/BotMessageRouting/MessageRouting/MessageRouterManager.cs)** is the main
class of the project. It manages the routing data (using the provided `IRoutingDataManager`
implementation) and executes the actual message mediation between the parties connected in a
conversation.

##### Properties #####

* `RoutingDataManager`: The implementation of the `IRoutingDataManager` interface in use. This
  property is set by passing the instance to the constructor of the `MessageRouterManager` class.
  This project provides two implementations of this interface: `LocalRoutingDataManager` for testing
  and `AzureTableStorageRoutingDataManager`. You can implement your own routing data management by
  using the `IRoutingDataManager` interface as the base or deriving from
  `AbstractRoutingDataManager`.

##### Methods #####

* **`HandleActivityAsync`**: In *very simple* cases this is the only method you need to call (in
  addition to `Diconnect`). It will track the users (stores their information), forward messages
  between users connected in a conversation automatically. The return value (`MessageRouterResult`)
  will indicate whether the message routing logic consumed the activity or not. If the activity was
  ignored by the message routing logic, you can e.g. forward it to your dialog.
* `SendMessageToPartyByBotAsync`: Utility method to make the bot send a given message to a given user.
* `BroadcastMessageToAggregationChannelsAsync`: Sends the given message to all the aggregation channels.
* `MakeSurePartiesAreTracked`: A convenient method for adding parties. The given parties are added,
  if they are new. This method is called by `HandleActivityAsync` so you don't need to bother
  calling this explicitly yourself unless your bot code is a bit more complex.
* `RemoveParty`: Removes all the instances related to the given party from the routing data (since
  there can be multiple - one for each conversation). Will also remove any pending requests of the
  party in question as well as end all conversations of this specific user.
* `RequestConnection`: Creates a request on behalf of the given party/sender of the activity.
* `RejectPendingRequest`: Removes the pending request of the given user.
* `ConnectAsync`: Establishes a connection between the given parties. This method should be called
  when a chat request is accepted (given that requests are necessary).
* `Disconnect`: Ends the conversation and severs the connection between the users so that the
  messages are no longer relayed.
* `RouteMessageIfSenderIsConnectedAsync`: Relays the messages between connected parties.

#### Other classes ####

**[AbstractRoutingDataManager](/BotMessageRouting/MessageRouting/DataStore/AbstractRoutingDataManager.cs)**
implements the `IRoutingDataManager` interface partially and is the base class for both
`LocalRoutingDataManager` and `AzureTableStorageRoutingDataManager`. It contains the main logic for
routing data management while leaving the storage specific operation implementations
(add, remove, etc.) for the subclasses.

**[AzureTableStorageRoutingDataManager](/BotMessageRouting/MessageRouting/DataStore/Azure/AzureTableStorageRoutingDataManager.cs)**
implements the `IRoutingDataManager` interface. The constructor takes the connection string to your
Azure Storage.

**[LocalRoutingDataManager](/BotMessageRouting/MessageRouting/DataStore/Local/LocalRoutingDataManager.cs)**
implements the `IRoutingDataManager` interface. Note that this class is meant for testing and
provides only an in-memory solution. **Do not use this class in production!**

**[Party](/BotMessageRouting/Models/Party.cs)** holds the details of specific user/bot in a specific
conversation. One can think of `Party` as a full address the bot needs in order to send a message to
the user in a conversation. The `Party` instances are stored in the routing data.

**[MessageRouterResult](/BotMessageRouting/MessageRouting/MessageRouterResult.cs)** is the return
value for more complex operations of the `MessageRouterManager` class and methods of the
`IRoutingDataManager` implementation. You are responsible to handle these in your bot code.

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
* [Jamie D](https://github.com/daltskin)
* [Lucas Humenhuk](https://github.com/lcarli)
* [Lilian Kasem](https://github.com/liliankasem)
* [Edouard Mathon](https://github.com/edouard-mathon)
* [Gary Pretty](https://github.com/garypretty)

Note that you may not find (all of their) contributions in the change history of this project,
because all of the code including this core functionality used to reside in
[Intermediator Bot Sample](https://github.com/tompaana/intermediator-bot-sample) project.
