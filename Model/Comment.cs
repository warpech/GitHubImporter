using System;
using Starcounter;

namespace GitHubImporter {
    [Database]
    public class Comment {
        public Issue Issue;
        public User Author;
        public string Body;

        public DateTime CreatedAt;
        public DateTime UpdatedAt;
    }
}
