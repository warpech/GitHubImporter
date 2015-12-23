using Octokit;
using Starcounter;

namespace GitHubImporter {
    public class Helper {
        public static User GetOrCreateUser(string name) {
            User user = Db.SQL<User>("SELECT u FROM GitHubImporter.User u WHERE u.Name = ?", name).First;
            if (user == null) {
                Db.Transact(() => {
                    user = new User {
                        Name = name
                    };
                });
            }
            return user;
        }

        public static Repository GetOrCreateRepository(User owner, string name) {
            Repository repository = Db.SQL<Repository>("SELECT r FROM GitHubImporter.Repository r WHERE r.Owner = ? AND r.Name = ?", owner, name).First;
            if (repository == null) {
                Db.Transact(() => {
                    repository = new Repository {
                        Owner = owner,
                        Name = name
                    };
                });
            }
            return repository;
        }

        public static Issue GetOrCreateIssue(Repository repository, int externalId) {
            Issue issue = Db.SQL<Issue>("SELECT i FROM GitHubImporter.Issue i WHERE i.Repository = ? AND i.ExternalId = ?", repository, externalId).First;
            if (issue == null) {
                Db.Transact(() => {
                    issue = new Issue {
                        Repository = repository,
                        ExternalId = externalId
                    };
                });
            }
            return issue;
        }

        public static IssueEvent CreateIssueEvent(Issue issue, EventInfo eventInfo) {
            IssueEvent issueEvent = null;
                Db.Transact(() => {
                    issueEvent = new IssueEvent {
                        Issue = issue,
                        ExternalId = eventInfo.Id
                    };
                    //eventInfo.Label
                });
            return issueEvent;
        }
    }
}
