using Microsoft.WindowsAzure.Storage.Table;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace BotMessageRouting.Models.Azure
{
    public class RoutingDataEntity : TableEntity
    {
        public string Body { get; set; }
    }
}
