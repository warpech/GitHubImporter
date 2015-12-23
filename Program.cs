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

            Db.Transact(() => {
                Db.SlowSQL("DELETE FROM GitHubImporter.Comment");
                Db.SlowSQL("DELETE FROM GitHubImporter.Issue");
                Db.SlowSQL("DELETE FROM GitHubImporter.IssueEvent");
                Db.SlowSQL("DELETE FROM GitHubImporter.Label");
                Db.SlowSQL("DELETE FROM GitHubImporter.Repository");
                Db.SlowSQL("DELETE FROM GitHubImporter.User");

            });

            Console.WriteLine("Connecting to GH...");
            var tokenAuth = new Credentials("86d76bd01f7dca15036c0ed3b0c84e784727b554");
            github = new GitHubClient(new ProductHeaderValue("GitHubImporter"));
            github.Credentials = tokenAuth;

            LoadStartData();

        }

        static async void LoadStartData() {
            User user = Helper.GetOrCreateUser("Starcounter");
            Repository repository = Helper.GetOrCreateRepository(user, "Replicator");

            byte schedulerId = StarcounterEnvironment.CurrentSchedulerId;
            var ghIssues = await GetIssues(repository);
            new DbSession().RunSync(() => {
                StarcounterEnvironment.RunWithinApplication(appName, () => {
                    SaveIssues(repository, ghIssues);

                    var issue = FindOutdatedIssue(repository);
                    if (issue != null) {
                        schedulerId = StarcounterEnvironment.CurrentSchedulerId;
                        string ownerName = issue.Repository.Owner.Name;
                        string repositoryName = issue.Repository.Name;
                        int number = issue.ExternalId;
                        Task.Run(async () => {
                            var ghEvents = await GetEventInfos(ownerName, repositoryName, number);

                            if (ghEvents != null) {
                                new DbSession().RunSync(() => {
                                    StarcounterEnvironment.RunWithinApplication(appName, () => {
                                        Console.WriteLine("Got event info for #" + issue.ExternalId + " : " + ghEvents.Count);
                                        foreach (var ghEvent in ghEvents) {
                                            Helper.CreateIssueEvent(issue, ghEvent);
                                        }
                                    });
                                }, schedulerId);
                            }
                        });
                    }

                });
            }, schedulerId);
        }

        static async Task<IReadOnlyList<Octokit.EventInfo>> GetEventInfos(string ownerName, string repositoryName, int number) {
            IReadOnlyList<Octokit.EventInfo> ghEvents = null;


            try {
                //ghEvents = await github.Issue.Events.GetAllForIssue(issue.Repository.Owner.Name, issue.Repository.Name, issue.ExternalId);
                ghEvents = await github.Issue.Events.GetAllForIssue(ownerName, repositoryName, number);
                //ghEvents = await github.Issue.Events.GetAllForIssue("Starcounter", "Starcounter", 1);
            }

            catch (Exception ex) {
                    StarcounterEnvironment.RunWithinApplication(appName, () => {
                        Console.WriteLine("Exception caught: " + ex);
                    });
            }
            return ghEvents;
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

        static Issue FindOutdatedIssue(Repository repository) {
            var issue = Db.SQL<Issue>("SELECT i FROM Issue i WHERE i.Repository = ? ORDER BY i.CheckedAt ASC FETCH ?", repository, 1).First;
            return issue;
        }
    }
}