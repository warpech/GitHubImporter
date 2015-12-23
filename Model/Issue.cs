using System;
using Starcounter;

namespace GitHubImporter {
    [Database]
    public class Issue {
        public Repository Repository;
        public User Author;

        public int ExternalId;
        public string Title;
        public string Body;
        public string Url;

        public DateTime CreatedAt;
        public DateTime ClosedAt;
        public DateTime UpdatedAt;
        public DateTime EventsCheckedAt;

        public QueryResultRows<Comment> Comments {
            get {
                return Db.SQL<Comment>("SELECT c FROM GitHubImporter.Comment c WHERE c.Issue = ?", this);
            }
        }
    }
}
