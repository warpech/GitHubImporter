using System;
using Starcounter;
using Octokit;
using Starcounter.Internal;

namespace GitHubImporter {
    class Program {

        static void Main() {
            Console.WriteLine("Starting...");
            RefreshGitHubData();
        }

        static async void RefreshGitHubData() {
            var schedulerId = StarcounterEnvironment.CurrentSchedulerId;
            var appName = StarcounterEnvironment.AppName;

            try {
                Console.WriteLine("Connecting to GH...");
                var github = new GitHubClient(new ProductHeaderValue("GitHubImporter"));

                Console.WriteLine("Getting user info");
                var user = await github.User.Get("warpech");

                new DbSession().RunAsync(() => {
                    StarcounterEnvironment.RunWithinApplication(appName, () => {
                        Console.WriteLine("Saving to db");
                        Db.Transact(() => {
                            Console.WriteLine("Creating new User");
                            new User() {
                                Name = user.Name,
                                Url = user.HtmlUrl,
                                AvatarUrl = user.AvatarUrl
                            };
                        });
                    });
                }, schedulerId);
            }
            catch (Exception ex) {
                new DbSession().RunAsync(() => {
                    StarcounterEnvironment.RunWithinApplication(appName, () => {
                        Console.WriteLine("Exception caught: " + ex); //does not show in Administrator
                    });
                }, schedulerId);
            }
        }
    }
}