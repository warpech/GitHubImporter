using Octokit;
using Starcounter;
using Starcounter.Internal;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace GitHubImporter {
    public class GitHubApiHelper {
        static GitHubClient github;
        static string appName = StarcounterEnvironment.AppName;

        private static void CreateClient() {
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
        }

        public static async Task<IReadOnlyList<EventInfo>> GetEventInfos(string ownerName, string repositoryName, int number) {
            if (github == null) {
                CreateClient();
            }

            IReadOnlyList<EventInfo> ghEvents = null;
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

        public static async Task<IReadOnlyList<IssueComment>> GetComments(string ownerName, string repositoryName, int number) {
            if (github == null) {
                CreateClient();
            }

            IReadOnlyList<IssueComment> ghComments = null;
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

        public static async Task<IReadOnlyList<Octokit.Issue>> GetIssues(Repository repository) {
            if (github == null) {
                CreateClient();
            }

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

        public static async Task<MiscellaneousRateLimit> GetRateLimit() {
            if (github == null) {
                CreateClient();
            }

            MiscellaneousRateLimit ghRateLimit = null;

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
    }
}
