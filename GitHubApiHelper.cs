using Octokit;
using Starcounter;
using Starcounter.Internal;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace GitHubImporter {
    public class TokenEmptyException : Exception {

    }

    public class GitHubApiHelper {
        public GitHubClient Client;
        public Exception LastError;
        private string AppName = StarcounterEnvironment.AppName;

        public GitHubApiHelper() {
            Console.WriteLine("Connecting to GH...");
            CreateClient();
        }

        public void CreateClient() {
            Client = null;
            LastError = null;
            try {
                SettingsToken token = Db.SQL<SettingsToken>("SELECT s FROM SettingsToken s FETCH ?", 1).First;
                if (token.Token == "") {
                    Console.WriteLine("GH token is empty. Go to http://localhost:8080/githubimporter/settings");
                    throw new TokenEmptyException();
                }

                var tokenAuth = new Credentials(token.Token);
                Client = new GitHubClient(new ProductHeaderValue("GitHubImporter"));
                Client.Credentials = tokenAuth;
            }
            catch (Exception ex) {
                LastError = ex;
            }
        }

        public async Task<IReadOnlyList<EventInfo>> GetEventInfos(string ownerName, string repositoryName, int number) {
            LastError = null;
            IReadOnlyList<EventInfo> ghEvents = null;
            try {
                ghEvents = await Client.Issue.Events.GetAllForIssue(ownerName, repositoryName, number);
            }
            catch (Exception ex) {
                LastError = ex;
                StarcounterEnvironment.RunWithinApplication(AppName, () => {
                    Console.WriteLine("Exception caught: " + ex);
                });
            }
            return ghEvents;
        }

        public async Task<IReadOnlyList<IssueComment>> GetComments(string ownerName, string repositoryName, int number) {
            IReadOnlyList<IssueComment> ghComments = null;
            try {
                ghComments = await Client.Issue.Comment.GetAllForIssue(ownerName, repositoryName, number);
            }
            catch (Exception ex) {
                LastError = ex;
                StarcounterEnvironment.RunWithinApplication(AppName, () => {
                    Console.WriteLine("Exception caught: " + ex);
                });
            }
            return ghComments;
        }

        public async Task<IReadOnlyList<Octokit.Issue>> GetIssues(Repository repository) {
            LastError = null;
            IReadOnlyList<Octokit.Issue> ghIssues = null;

            try {
                var req = new RepositoryIssueRequest() {
                    State = ItemState.All,
                    SortDirection = SortDirection.Ascending
                };
                ghIssues = await Client.Issue.GetAllForRepository(repository.Owner.Name, repository.Name, req);

            }
            catch (Exception ex) {
                LastError = ex;
                StarcounterEnvironment.RunWithinApplication(AppName, () => {
                    Console.WriteLine("Exception caught: " + ex);
                });
            }

            return ghIssues;
        }

        public async Task<MiscellaneousRateLimit> GetRateLimit() {
            LastError = null;
            MiscellaneousRateLimit ghRateLimit = null;

            try {
                ghRateLimit = await Client.Miscellaneous.GetRateLimits();

            }
            catch (Exception ex) {
                LastError = ex;
                StarcounterEnvironment.RunWithinApplication(AppName, () => {
                    Console.WriteLine("Exception caught: " + ex);
                });
            }

            return ghRateLimit;
        }
    }
}
