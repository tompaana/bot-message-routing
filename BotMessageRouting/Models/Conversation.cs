using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using Microsoft.Bot.Connector;
using Microsoft.WindowsAzure.Storage.Table;
using Underscore.Bot.Models;


namespace Underscore.Bot.Models

{
    public class Conversation : TableEntity
    {
        public Conversation(string PartitionKey, string RowKey)
        {
            this.PartitionKey = PartitionKey;
            this.RowKey = RowKey;
        }

        public Conversation() { }

        public string Owner { get; set; }

        public string Client { get; set; }
    }
}