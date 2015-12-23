using System;
using Starcounter;
using Octokit;

namespace GitHubImporter {
    [Database]
    public class IssueEvent {
        public Issue Issue;
        public int ExternalId;
        //public EventType
        //public EventInfo EventInfo;
    }
}
