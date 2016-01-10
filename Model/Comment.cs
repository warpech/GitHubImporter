using System;
using Starcounter;

namespace GitHubImporter {
    [Database]
    public class Comment {
        public Issue Issue;
        public int ExternalId;
        public User Author;
        public string Body;

        public DateTime CreatedAt;
        public DateTime UpdatedAt;

        public float Sentiment;

        public Comment() {
            Sentiment = -1;
        }
    }
}
