// Copyright (c) Microsoft Corporation. All rights reserved.

using Microsoft.CloudMine.GitHub.Collectors.Model;
using System;

namespace Microsoft.CloudMine.GitHub.Collectors.Cache
{
    public class EventsTimelineTableEntity : RepositoryTableEntity
    {
        public string SessionId { get; set; }
        public long LastSeenEventId { get; set; } = long.MinValue;
        public DateTime LastSeenEventDate { get; set; } = DateTime.MinValue;

        public EventsTimelineTableEntity()
        {
        }

        public EventsTimelineTableEntity(Repository repository, string sessionId, long lastSeenEventId, DateTime lastSeenEventDate)
            : base(repository)
        {
            this.RowKey = string.Empty;

            this.LastSeenEventId = lastSeenEventId;
            this.SessionId = sessionId;
            this.LastSeenEventDate = lastSeenEventDate;
        }
    }
}
