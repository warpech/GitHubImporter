using System;
using Starcounter;
using Octokit;
using Starcounter.Internal;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;

namespace GitHubImporter {
    public class ReportInformation {
        public string Name;
        public Int64 Count;
    }

    class Program {

        static GitHubClient github;


        static string appName = StarcounterEnvironment.AppName;

        static void Main() {
            Console.WriteLine("Starting...");

            Handle.GET("/githubimporter", () => {
                var master = new Master() {
                    Data = Db.SQL<Repository>("SELECT r FROM Repository r").First
                };

                var report = new Report() { };
                report.Title = "Most active commenters";

                var users = Db.SQL<User>("SELECT u FROM User u");
                List<ReportInformation> list = new List<ReportInformation>();
                foreach(var user in users) {
                    var item = new ReportInformation();
                    item.Name = user.Name;
                    item.Count = Db.SQL<Int64>("SELECT COUNT(c) FROM Comment c WHERE c.Author = ?", user).First;
                    list.Add(item);
                }

                report.Items.Data = list.OrderByDescending(x => x.Count);

                master.Report = report;

                return master;
            });
            

            //LoadStartData();
        }

        static async void LoadStartData() {
            Db.Transact(() => {
                Db.SlowSQL("DELETE FROM GitHubImporter.Comment");
                Db.SlowSQL("DELETE FROM GitHubImporter.Issue");
                Db.SlowSQL("DELETE FROM GitHubImporter.IssueEvent");
                Db.SlowSQL("DELETE FROM GitHubImporter.IssueEventType");
                Db.SlowSQL("DELETE FROM GitHubImporter.Label");
                Db.SlowSQL("DELETE FROM GitHubImporter.Repository");
                Db.SlowSQL("DELETE FROM GitHubImporter.User");

                new IssueEventType() {
                    Name = "Closed"
                };
                new IssueEventType() {
                    Name = "Reopened"
                };
                new IssueEventType() {
                    Name = "Subscribed"
                };
                new IssueEventType() {
                    Name = "Merged"
                };
                new IssueEventType() {
                    Name = "Referenced"
                };
                new IssueEventType() {
                    Name = "Mentioned"
                };
                new IssueEventType() {
                    Name = "Assigned"
                };
                new IssueEventType() {
                    Name = "Unassigned"
                };
                new IssueEventType() {
                    Name = "Labeled"
                };
                new IssueEventType() {
                    Name = "Unlabeled"
                };
                new IssueEventType() {
                    Name = "Milestoned"
                };
                new IssueEventType() {
                    Name = "Demilestoned"
                };
                new IssueEventType() {
                    Name = "Renamed"
                };
                new IssueEventType() {
                    Name = "Locked"
                };
                new IssueEventType() {
                    Name = "Unlocked"
                };
                new IssueEventType() {
                    Name = "HeadRefDeleted"
                };
                new IssueEventType() {
                    Name = "HeadRefRestored"
                };
                new IssueEventType() {
                    Name = "Unsubscribed"
                };
            });

            Console.WriteLine("Connecting to GH...");
            var tokenAuth = new Credentials("86d76bd01f7dca15036c0ed3b0c84e784727b554");
            github = new GitHubClient(new ProductHeaderValue("GitHubImporter"));
            github.Credentials = tokenAuth;


            User user = Helper.GetOrCreateUser("Starcounter", null, null);
            Repository repository = Helper.GetOrCreateRepository(user, "Replicator");

            byte schedulerId = StarcounterEnvironment.CurrentSchedulerId;
            var ghIssues = await GetIssues(repository);
            new DbSession().RunSync(() => {
                StarcounterEnvironment.RunWithinApplication(appName, () => {
                    SaveIssues(repository, ghIssues);
                    schedulerId = StarcounterEnvironment.CurrentSchedulerId;
                    Task.Run(async () => {
                        await UpdateNextIssueInfo(repository, schedulerId);
                    });

                });
            }, schedulerId);
        }

        static async Task<bool> UpdateNextIssueInfo(Repository repository, byte schedulerId) {
            new DbSession().RunSync(() => {
                StarcounterEnvironment.RunWithinApplication(appName, () => {

                    var issue = FindOutdatedIssueEvents(repository);

                    if (issue != null) {
                        Console.WriteLine("Found outdated issue events " + issue.ExternalId);
                        string ownerName = issue.Repository.Owner.Name;
                        string repositoryName = issue.Repository.Name;
                        int number = issue.ExternalId;
                        DateTime requestTime = DateTime.UtcNow;

                        Task.Run(async () => {
                            var ghEvents = await GetEventInfos(ownerName, repositoryName, number);


                            new DbSession().RunSync(() => {
                                StarcounterEnvironment.RunWithinApplication(appName, () => {
                                    if (ghEvents != null) {
                                        Console.WriteLine("Got event info for #" + issue.ExternalId + " : " + ghEvents.Count);
                                        foreach (var ghEvent in ghEvents) {
                                            Helper.CreateIssueEvent(issue, ghEvent);
                                        }
                                        Db.Transact(() => {
                                            issue.EventsCheckedAt = requestTime;
                                        });
                                    }
                                    schedulerId = StarcounterEnvironment.CurrentSchedulerId;
                                    Task.Run(async () => {
                                        await UpdateNextIssueInfo(repository, schedulerId);
                                    });
                                });
                            }, schedulerId);

                        });

                    }
                    else {
                        issue = FindOutdatedIssueComments(repository);

                        if (issue != null) {

                            Console.WriteLine("Found outdated issue comments " + issue.ExternalId);
                            string ownerName = issue.Repository.Owner.Name;
                            string repositoryName = issue.Repository.Name;
                            int number = issue.ExternalId;
                            DateTime requestTime = DateTime.UtcNow;

                            Task.Run(async () => {
                                var ghComments = await GetComments(ownerName, repositoryName, number);


                                new DbSession().RunSync(() => {
                                    StarcounterEnvironment.RunWithinApplication(appName, () => {
                                        if (ghComments != null) {
                                            Console.WriteLine("Got comments for #" + issue.ExternalId + " : " + ghComments.Count);
                                            foreach (var ghComment in ghComments) {
                                                Helper.CreateComment(issue, ghComment);
                                            }
                                            Db.Transact(() => {
                                                issue.CommentsCheckedAt = requestTime;
                                            });
                                        }
                                        schedulerId = StarcounterEnvironment.CurrentSchedulerId;
                                        Task.Run(async () => {
                                            await UpdateNextIssueInfo(repository, schedulerId);
                                        });
                                    });
                                }, schedulerId);

                            });
                        }
                    }
                });
            }, schedulerId);
            return true;
        }

        static async Task<IReadOnlyList<Octokit.EventInfo>> GetEventInfos(string ownerName, string repositoryName, int number) {
            IReadOnlyList<Octokit.EventInfo> ghEvents = null;
            try {
                ghEvents = await github.Issue.Events.GetAllForIssue(ownerName, repositoryName, number);
            }
            catch (Exception ex) {
                StarcounterEnvironment.RunWithinApplication(appName, () => {
                    Console.WriteLine("Exception caught: " + ex);
                });
            }
            return ghEvents;
        }

        static async Task<IReadOnlyList<Octokit.IssueComment>> GetComments(string ownerName, string repositoryName, int number) {
            IReadOnlyList<Octokit.IssueComment> ghComments = null;
            try {
                ghComments = await github.Issue.Comment.GetAllForIssue(ownerName, repositoryName, number);
            }
            catch (Exception ex) {
                StarcounterEnvironment.RunWithinApplication(appName, () => {
                    Console.WriteLine("Exception caught: " + ex);
                });
            }
            return ghComments;
        }

        static async Task<IReadOnlyList<Octokit.Issue>> GetIssues(Repository repository) {
            IReadOnlyList<Octokit.Issue> ghIssues = null;

            try {
                var req = new RepositoryIssueRequest() {
                    State = ItemState.All,
                    SortDirection = SortDirection.Ascending
                };
                ghIssues = await github.Issue.GetAllForRepository(repository.Owner.Name, repository.Name, req);

            }
            catch (Exception ex) {
                StarcounterEnvironment.RunWithinApplication(appName, () => {
                    Console.WriteLine("Exception caught: " + ex);
                });
            }

            return ghIssues;
        }

        static void SaveIssues(Repository repository, IReadOnlyList<Octokit.Issue> ghIssues) {
            Console.WriteLine("Saving to so many issues: " + ghIssues.Count);
            foreach (var issue in ghIssues) {
                Helper.GetOrCreateIssue(repository, issue.Number);
            }
        }

        static Issue FindOutdatedIssueEvents(Repository repository) {
            var issue = Db.SQL<Issue>("SELECT i FROM Issue i WHERE i.Repository = ? ORDER BY i.EventsCheckedAt ASC FETCH ?", repository, 1).First;

            var diffInMinutes = (DateTime.UtcNow - issue.EventsCheckedAt).TotalMinutes;

            if (diffInMinutes > 60) {
                return issue;
            }
            else {
                return null;
            }
        }

        static Issue FindOutdatedIssueComments(Repository repository) {
            var issue = Db.SQL<Issue>("SELECT i FROM Issue i WHERE i.Repository = ? ORDER BY i.CommentsCheckedAt ASC FETCH ?", repository, 1).First;

            var diffInMinutes = (DateTime.UtcNow - issue.CommentsCheckedAt).TotalMinutes;

            if (diffInMinutes > 60) {
                return issue;
            }
            else {
                return null;
            }
        }
    }
}