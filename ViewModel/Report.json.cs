//using Deedle;
using Starcounter;
using System;
using System.Collections.Generic;
using System.Linq;

namespace GitHubImporter {
    public class ReportInformation {
        public string Name;
        public string Url;
        public string AvatarUrl;
        public Int64 Count;
    }

    partial class Report : Partial {
        public void RefreshReport() {
            if (Db.SQL("SELECT i FROM Starcounter.Metadata.\"Index\" i WHERE Name = ?", "GitHubImporterIssueStatus").First == null) {
                //Db.SQL("CREATE INDEX GitHubImporterIssueStatus ON Issue (Status ASC)");
            }
            else {
                //Db.SQL("DROP INDEX GitHubImporterIssueStatus ON Issue");
            }
            if (Db.SQL("SELECT i FROM Starcounter.Metadata.\"Index\" i WHERE Name = ?", "GitHubImporterCommentAuthor").First == null) {
                Db.SQL("CREATE INDEX GitHubImporterCommentAuthor ON Comment (Author ASC)");
            }
            else {
                //Db.SQL("DROP INDEX GitHubImporterCommentAuthor ON Comment");
            }

            var users = Db.SQL<User>("SELECT u FROM User u");
            List<ReportInformation> list = new List<ReportInformation>();
            foreach (var user in users) {
                Int64 count;
                QueryBuilder whereBuilder = new QueryBuilder();
                Setup.BuildWhere(whereBuilder);
                whereBuilder.Where("c.Author = ?", user);
                count = Db.SQL<Int64>("SELECT COUNT(c) FROM Comment c WHERE " + whereBuilder.Query, whereBuilder.Params).First;

                if (count > 0) {
                    var item = new ReportInformation();
                    item.Name = user.Name;
                    item.Url = user.Url;
                    item.AvatarUrl = user.AvatarUrl;
                    item.Count = count;
                    list.Add(item);
                }
            }

            Title = "Most active commenters";
            Items.Clear();
            Items.Data = list.OrderByDescending(x => x.Count);


            /*var comments = Db.SQL<Comment>("SELECT c FROM Comment c FETCH ?", 100);
            var series = new SeriesBuilder<DateTime, string>();
            foreach (var comment in comments) {
                series.Add(comment.CreatedAt, comment.Author.Name);
            }

            series.Series.Print();*/
        }
    }
}
