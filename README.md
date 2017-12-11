Bot Message Routing (component)
===============================

This project is a message routing component for chatbots built with
[Microsoft Bot Framework](https://dev.botframework.com/) C# SDK. It enables routing messages between
users on different channels. In addition, it can be used in advanced customer service scenarios
where the normal routines are handled by a bot, but in case the need arises, the customer can be
connected with a human customer service agent.

**For an example on how to take this code into use, see
[Intermediator Bot Sample](https://github.com/tompaana/intermediator-bot-sample).**

This project is also available as
[NuGet package](https://www.nuget.org/packages/Underscore.Bot.MessageRouting).

Don't worry, if you prefer the Node.js SDK; in that case check out
[this sample](https://github.com/palindromed/Bot-HandOff)!

### Possible use cases ###

* Routing messages between users/bots
    * See also: [Chatbots as Middlemen blog post](http://tomipaananen.azurewebsites.net/?p=1851)
* Customer service scenarios where (in tricky cases) the customer requires a human customer service agent
* Keeping track of users the bot interacts with
* Sending notifications
    * For more information see [this blog post](http://tomipaananen.azurewebsites.net/?p=2231) and
      [this sample](https://github.com/tompaana/remote-control-bot-sample))

## Implementation ##

### Terminology ###

| Term | Description |
| ---- | ----------- |
| Aggregation (channel) | **Only applies if aggregation approach is used!** A channel where the chat requests are sent. The users in the aggregation channel can accept the requests. |
| Connection | Is created when a request is accepted - the acceptor and the one accepted form a connection (1:1 chat where the bot relays the messages between the users). |
| Party | A user/bot in a specific conversation. |
| Conversation client | A reqular user e.g. a customer. |
| Conversation owner | E.g. a customer service **agent**. |

### Interfaces ###

#### [IRoutingDataManager](/BotMessageRouting/MessageRouting/DataStore/IRoutingDataManager.cs) ####

An interface for managing the parties (users/bot), aggregation channel details, the list of
connected parties and pending requests. **Note:** In production this data should be stored in e.g.
a table storage!
[LocalRoutingDataManager](/BotMessageRouting/MessageRouting/DataStore/LocalRoutingDataManager.cs)
is provided for testing, but it provides only an in-memory solution.

#### [IMessageRouterResultHandler](/BotMessageRouting/MessageRouting/IMessageRouterResultHandler.cs) ####

An interface for handling message router operation results. You can consider the result handler as
an event handler, but since asynchronicity (that comes with an actual event handler in C#) may cause
all kind of problems with the bot framework *(namely that the execution of the code that makes the
bot send messages related to an incoming activity should not continue once `MessagesController.Post`
is exited)*, it is more convenient to handle the results this way. The implementation of this
interface defines the bot responses for specific events (results) so it is a natural place to have
localization in (should your bot application require it).

### MessageRouterManager class ###

**[MessageRouterManager](/BotMessageRouting/MessageRouting/MessageRouterManager.cs)** is the main
class of the project. It manages the routing data (using the provided `IRoutingDataManager`
implementation) and executes the actual message mediation between the parties connected in a
conversation.

#### Properties ####

* `RoutingDataManager`: The implementation of `IRoutingDataManager` interface
  in use. In case you want to replace the default implementation with your own,
  set it in `App_Start\WebApiConfig.cs`.

#### Methods ####

* **`HandleActivityAsync`**: In *very simple* cases this is the only method you need to call in
  your `MessagesController` class. It will track the users (stores their information), forward
  messages between users connected in a conversation automatically. The return value
  (`MessageRouterResult`) will indicate whether the message routing logic consumed the activity or
  not. If the activity was ignored by the message routing logic, you can e.g. forward it to your
  dialog.
* `SendMessageToPartyByBotAsync`: Utility method to make the bot send a given message to a given user.
* `BroadcastMessageToAggregationChannelsAsync`: Sends the given message to all the aggregation channels.
* `MakeSurePartiesAreTracked`: A convenient method for adding parties.  The given parties are added,
  if they are new. This method is called by `HandleActivityAsync` so you don't need to bother
  calling this explicitly yourself.
* `RemoveParty`: Removes all the instances related to the given party from the routing data (since
  there can be multiple - one for each conversation). Will also remove any pending requests of the
  party in question as well as end all conversations of this specific user.
* `RequestConnection`: Creates a request on behalf of the sender of the activity.
* `RejectPendingRequest`: Removes the pending request of the given user.
* `ConnectAsync`: Establishes a connection between the given parties. This method should be called
  when a chat request is accepted (given that requests are necessary).
* `Disconnect`: Ends the conversation and severs the connection between the users so that the
  messages are no longer relayed.
* `HandleMessageAsync`: Handles the incoming messages: Relays the messages between connected parties.

### Other classes ###

**[Party](/BotMessageRouting/Models/Party.cs)** holds the details of specific user/bot in a specific
conversation. Note that the bot collects parties from all the conversations it's in and there will
be a `Party` instance of a user/bot for each conversation (i.e. there can be multiple parties for a
single user/bot). One can think of `Party` as a full address the bot needs in order to send a
message to the user in a conversation. The `Party` instances are stored in routing data.

**[PartyWithTimestamps](/BotMessageRouting/Models/PartyWithTimestamps.cs)** - like `Party`, but with
timestamps to mark when a request was made and when a connection was established. Useful for
monitoring waiting times and durations of conversations.

**[MessageRouterResult](/BotMessageRouting/MessageRouting/MessageRouterResult.cs)** is the return
value for more complex operations of the `MessageRouterManager` class not unlike custom `EventArgs`
implementations, but due to the problems that using actual event handlers can cause, these return
values are handled by a dedicated `IMessageRouterResultHandler` implementation (not provided in this
project).

## Acknowledgements ##

Special thanks to the key contributors (in alphabetical order):

* [Jamie D](https://github.com/daltskin)
* [Lucas Humenhuk](https://github.com/lcarli)
* [Lilian Kasem](https://github.com/liliankasem)
* [Edouard Mathon](https://github.com/edouard-mathon)
* [Gary Pretty](https://github.com/garypretty)

Note that you may not find (all of their) contributions in the change history of this project,
because all of the code including this core functionality used to reside in
[Intermediator Bot Sample](https://github.com/tompaana/intermediator-bot-sample) project.
