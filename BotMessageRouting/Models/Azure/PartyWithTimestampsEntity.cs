using System;

namespace Underscore.Bot.Models.Azure
{
    public class PartyWithTimestampsEntity : PartyEntity
    {
        public DateTime ConnectionRequestTime { get; set; }
        public DateTime ConnectionEstablishedTime { get; set; }

        public PartyWithTimestampsEntity() : base()
        {
            ResetConnectionRequestTime();
            ResetConnectionEstablishedTime();
        }

        public PartyWithTimestampsEntity(string partitionKey, string rowKey) : base(partitionKey, rowKey)
        {
            ResetConnectionRequestTime();
            ResetConnectionEstablishedTime();
        }

        public PartyWithTimestampsEntity(PartyWithTimestamps partyWithTimestamps, PartyEntityType partyEntityType)
            : base(partyWithTimestamps, partyEntityType)
        {
            ConnectionRequestTime = partyWithTimestamps.ConnectionRequestTime;
            ConnectionEstablishedTime = partyWithTimestamps.ConnectionEstablishedTime;
        }

        public void ResetConnectionRequestTime()
        {
            ConnectionRequestTime = DateTime.MinValue;
        }

        public void ResetConnectionEstablishedTime()
        {
            ConnectionEstablishedTime = DateTime.MinValue;
        }
    }
}   
