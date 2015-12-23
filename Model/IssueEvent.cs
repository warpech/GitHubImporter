using System;
using Starcounter;
using Octokit;

namespace GitHubImporter {
    [Database]
    public class IssueEvent {
        public Issue Issue;
        public int ExternalId;
        public IssueEventType Type;
        public User Actor;
        public User Assignee;
        public Label Label;
        public string CommitId;
    }
}
