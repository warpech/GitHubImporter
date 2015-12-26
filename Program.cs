﻿using System;
using Starcounter;
using Starcounter.Internal;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace GitHubImporter {
    class Program {
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
                
                master.CurrentPage = Db.Scope<Json>(() => {
                    var settings = new Settings();
                    settings.Token.Data = token;
                    settings.Token.RefreshLimits();
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

            User user = Helper.GetOrCreateUser("Starcounter", null, null);
            Repository repository = Helper.GetOrCreateRepository(user, "Starcounter");

            byte schedulerId = StarcounterEnvironment.CurrentSchedulerId;
            var ghIssues = await GitHubApiHelper.GetIssues(repository);
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
                            var ghEvents = await GitHubApiHelper.GetEventInfos(ownerName, repositoryName, number);

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
                                var ghComments = await GitHubApiHelper.GetComments(ownerName, repositoryName, number);

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

        static void SaveIssues(Repository repository, IReadOnlyList<Octokit.Issue> ghIssues) {
            Console.WriteLine("Saving to so many issues: " + ghIssues.Count);
            foreach (var issue in ghIssues) {
                Helper.GetOrCreateIssue(repository, issue.Number);
            }
        }

        static Issue FindOutdatedIssueEvents(Repository repository) {
            var issue = Db.SQL<Issue>("SELECT i FROM Issue i WHERE i.Repository = ? ORDER BY i.EventsCheckedAt ASC FETCH ?", repository, 1).First;

            var diffInDays = (DateTime.UtcNow - issue.EventsCheckedAt).TotalDays;

            if (diffInDays > 365 * 10) {
                //return issue;
                return null;
            }
            else {
                return null;
            }
        }

        static Issue FindOutdatedIssueComments(Repository repository) {
            var issue = Db.SQL<Issue>("SELECT i FROM Issue i WHERE i.Repository = ? ORDER BY i.CommentsCheckedAt ASC FETCH ?", repository, 1).First;

            var diffInDays = (DateTime.UtcNow - issue.CommentsCheckedAt).TotalDays;

            if (diffInDays > 365 * 10) {
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