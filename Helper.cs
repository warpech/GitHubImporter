using Octokit;
using Starcounter;
using System;

namespace GitHubImporter {
    public class Helper {
        public static User GetOrCreateUser(string name, string url, string avatarUrl) {
            User user = Db.SQL<User>("SELECT u FROM GitHubImporter.User u WHERE u.Name = ?", name).First;
            if (user == null) {
                Db.Transact(() => {
                    user = new User {
                        Name = name,
                        Url = url,
                        AvatarUrl = avatarUrl
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
            //Console.WriteLine("GetOrCreateIssue " + externalId);
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

        public static Label GetOrCreateLabel(Repository repository, string name, string color) {
            Label label = Db.SQL<Label>("SELECT o FROM GitHubImporter.Label o WHERE o.Repository = ? AND o.Name = ?", repository, name).First;
            if (label == null) {
                Db.Transact(() => {
                    label = new Label {
                        Repository = repository,
                        Name = name,
                        Color = color
                    };
                });
            }
            return label;
        }

        public static IssueEvent CreateIssueEvent(Issue issue, EventInfo eventInfo) {
            //Console.WriteLine("CreateIssueEvent " + eventInfo.Event.ToString());
            IssueEvent issueEvent = Db.SQL<IssueEvent>("SELECT e FROM IssueEvent e WHERE e.ExternalId = ?", eventInfo.Id).First;
            if (issueEvent == null) {
                IssueEventType type = Db.SQL<IssueEventType>("SELECT t FROM IssueEventType t WHERE t.Name = ?", eventInfo.Event.ToString()).First;
                if (type == null) {
                    throw new Exception("Event type not recognised: " + eventInfo.Event.ToString());
                }
                issueEvent = new IssueEvent {
                    Issue = issue,
                    ExternalId = eventInfo.Id,
                    Type = type
                };
                if (eventInfo.Actor != null) {
                    issueEvent.Actor = GetOrCreateUser(eventInfo.Actor.Login, eventInfo.Actor.HtmlUrl.ToString(), eventInfo.Actor.AvatarUrl.ToString());
                }
                if (eventInfo.Assignee != null) {
                    issueEvent.Assignee = GetOrCreateUser(eventInfo.Actor.Login, eventInfo.Actor.HtmlUrl.ToString(), eventInfo.Actor.AvatarUrl.ToString());
                }
                if (eventInfo.Label != null) {
                    issueEvent.Label = GetOrCreateLabel(issue.Repository, eventInfo.Label.Name, eventInfo.Label.Color);
                }
                if (eventInfo.CommitId != null) {
                    issueEvent.CommitId = eventInfo.CommitId;
                }
                if (type.Name == "Closed") {
                    issue.Status = Db.SQL<IssueStatus>("SELECT s FROM IssueStatus s WHERE s.Name = ? FETCH ?", "Closed", 1).First;
                }
                else if (type.Name == "Reopened") {
                    issue.Status = Db.SQL<IssueStatus>("SELECT s FROM IssueStatus s WHERE s.Name = ? FETCH ?", "Open", 1).First;
                }
            }
            return issueEvent;
        }

        public static Comment CreateComment(Issue issue, IssueComment ghComment) {
            //Console.WriteLine("CreateComment " + ghComment.Id);
            Comment comment = Db.SQL<Comment>("SELECT c FROM Comment c WHERE c.ExternalId = ?", ghComment.Id).First;
            if (comment == null) {
                comment = new Comment {
                    Issue = issue,
                    ExternalId = ghComment.Id,
                    Author = GetOrCreateUser(ghComment.User.Login, ghComment.User.HtmlUrl.ToString(), ghComment.User.AvatarUrl.ToString()),
                    Body = ghComment.Body,
                    CreatedAt = ghComment.CreatedAt.UtcDateTime
                };
                var updatedAt = ghComment.UpdatedAt;
                if (updatedAt.HasValue) {
                    comment.UpdatedAt = updatedAt.Value.UtcDateTime;
                }
            }
            return comment;
        }
    }
}
