using System;
using Starcounter;
using Octokit;

namespace GitHubImporter {
    class Program {

        static void Main() {
            Console.WriteLine("Starting...");
            RefreshGitHubData();
        }

        static async void RefreshGitHubData() {
            try {
                Console.WriteLine("Connecting to GH...");
                var github = new GitHubClient(new ProductHeaderValue("GitHubImporter"));

                Console.WriteLine("Getting user info");
                var user = await github.User.Get("warpech");

                Console.WriteLine("Saving to db"); //does not show in Administrator
                Db.Transact(() => {
                    Console.WriteLine("Creating new User"); //does not execute at all (or breakpoint does not stop here)
                    new User() {
                        Name = user.Name,
                        Url = user.HtmlUrl,
                        AvatarUrl = user.AvatarUrl
                    };
                }); 
            }
            catch (Exception ex) {
                Console.WriteLine("Exception caught: " + ex); //does not show in Administrator
            }
        }
    }
}