using System;
using Starcounter;
using Octokit;
using Starcounter.Internal;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace GitHubImporter {
    class Program {
        static GitHubClient github;
        static string appName = StarcounterEnvironment.AppName;

        static void Main() {
            Console.WriteLine("Starting...");

            Handle.GET("/githubimporter/master", () => {
                Session session = Session.Current;

                if (session != null && session.Data != null)
                    return session.Data;

                var master = new Master() {
                    Data = Db.SQL<Repository>("SELECT r FROM Repository r").First
                };

                return master;
            });

            Handle.GET("/githubimporter", () => {
                var master = Self.GET<Master>("/githubimporter/master");

                var report = new Report() { };
                report.Setup.Status.Changed += (object sender, EventArgs e) => {
                    report.RefreshReport();
                };
                report.Setup.Period.Changed += (object sender, EventArgs e) => {
                    report.RefreshReport();
                };

                report.RefreshReport();
                master.CurrentPage = report;

                return master;
            });

            Handle.GET("/githubimporter/settings", () => {
                var master = Self.GET<Master>("/githubimporter/master");

                SettingsToken token = Db.SQL<SettingsToken>("SELECT s FROM SettingsToken s FETCH ?", 1).First;

                byte schedulerId = StarcounterEnvironment.CurrentSchedulerId;
                var waiting = GetRateLimit();
                waiting.ContinueWith(a => {
                    new DbSession().RunSync(() => {
                        StarcounterEnvironment.RunWithinApplication(appName, () => {
                            Db.Transact(() => {
                                token.Remaining = waiting.Result.Resources.Core.Remaining;
                                token.Limit = waiting.Result.Resources.Core.Limit;
                                token.ResetAt = waiting.Result.Resources.Core.Reset.UtcDateTime;
                            });
                            var settings = master.CurrentPage as Settings;
                            settings.Token.IsLoading = false;
                            master.Session.CalculatePatchAndPushOnWebSocket();
                        });
                    }, schedulerId);
                });

                master.CurrentPage = Db.Scope<Json>(() => {
                    var settings = new Settings();
                    settings.Token.Data = token;
                    settings.Token.IsLoading = true;
                    return settings;
                });

                return master;
            });

            CreateConfig();
            LoadStartData();
        }

        static void CreateConfig() {
            Db.Transact(() => {
                IssueEventType type = Db.SQL<IssueEventType>("SELECT t FROM IssueEventType t FETCH ?", 1).First;
                if (type == null) {
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
                }

                IssueStatus status = Db.SQL<IssueStatus>("SELECT s FROM IssueStatus s FETCH ?", 1).First;
                if (status == null) {
                    new IssueStatus() {
                        Name = "Open"
                    };
                    new IssueStatus() {
                        Name = "Closed"
                    };
                }

                SettingsToken token = Db.SQL<SettingsToken>("SELECT s FROM SettingsToken s FETCH ?", 1).First;
                if (token == null) {
                    new SettingsToken();
                }
            });
        }

        static async void LoadStartData() {
            /*Db.Transact(() => {
                Db.SlowSQL("DELETE FROM GitHubImporter.Comment");
                Db.SlowSQL("DELETE FROM GitHubImporter.Issue");
                Db.SlowSQL("DELETE FROM GitHubImporter.IssueEvent");
                Db.SlowSQL("DELETE FROM GitHubImporter.Label");
                Db.SlowSQL("DELETE FROM GitHubImporter.Repository");
                Db.SlowSQL("DELETE FROM GitHubImporter.User");
            });*/

            Console.WriteLine("Connecting to GH...");
            SettingsToken token = Db.SQL<SettingsToken>("SELECT s FROM SettingsToken s FETCH ?", 1).First;
            if (token == null) {
                Console.WriteLine("GH token missing. Go to http://localhost:8080/githubimporter/settings");
                return;
            }
            if (token.Token == null || token.Token == "") {
                Console.WriteLine("GH token is empty. Go to http://localhost:8080/githubimporter/settings");
                return;
            }

            var tokenAuth = new Credentials(token.Token);
            github = new GitHubClient(new ProductHeaderValue("GitHubImporter"));
            github.Credentials = tokenAuth;

            User user = Helper.GetOrCreateUser("Starcounter", null, null);
            Repository repository = Helper.GetOrCreateRepository(user, "Starcounter");

            byte schedulerId = StarcounterEnvironment.CurrentSchedulerId;
            //var ghIssues = await GetIssues(repository);
            new DbSession().RunSync(() => {
                StarcounterEnvironment.RunWithinApplication(appName, () => {
                    //SaveIssues(repository, ghIssues);
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
                                        Db.Transact(() => {
                                            foreach (var ghEvent in ghEvents) {
                                                Helper.CreateIssueEvent(issue, ghEvent);
                                            }
                                            issue.EventsCheckedAt = requestTime;
                                        });
                                        schedulerId = StarcounterEnvironment.CurrentSchedulerId;
                                        Task.Run(async () => {
                                            await UpdateNextIssueInfo(repository, schedulerId);
                                        });
                                    }
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
                                            Db.Transact(() => {
                                                foreach (var ghComment in ghComments) {
                                                    Helper.CreateComment(issue, ghComment);
                                                }
                                                issue.CommentsCheckedAt = requestTime;
                                            });
                                            schedulerId = StarcounterEnvironment.CurrentSchedulerId;
                                            Task.Run(async () => {
                                                await UpdateNextIssueInfo(repository, schedulerId);
                                            });
                                        }
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

        static async Task<Octokit.MiscellaneousRateLimit> GetRateLimit() {
            Octokit.MiscellaneousRateLimit ghRateLimit = null;

            try {
                ghRateLimit = await github.Miscellaneous.GetRateLimits();

            }
            catch (Exception ex) {
                StarcounterEnvironment.RunWithinApplication(appName, () => {
                    Console.WriteLine("Exception caught: " + ex);
                });
            }

            return ghRateLimit;
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

            var diffInDays = (DateTime.UtcNow - issue.EventsCheckedAt).TotalDays;

            if (diffInDays > 5) {
                return issue;
            }
            else {
                return null;
            }
        }

        static Issue FindOutdatedIssueComments(Repository repository) {
            var issue = Db.SQL<Issue>("SELECT i FROM Issue i WHERE i.Repository = ? ORDER BY i.CommentsCheckedAt ASC FETCH ?", repository, 1).First;

            var diffInDays = (DateTime.UtcNow - issue.CommentsCheckedAt).TotalDays;

            if (diffInDays > 5) {
                return issue;
            }
            else {
                return null;
            }
        }
    }
}



//TODO add job queue (event/comment info)
//add progress bar
//add current status