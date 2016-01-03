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

    public class WhereBuilder {
        public string where = "";
        public List<dynamic> whereParams = new List<dynamic>();
         
        public void Add(string addition, dynamic additionParam) {
            if(where != "") {
                where += "AND ";
            }
            where += addition + " ";
            whereParams.Add(additionParam);
        }
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
            var open = Db.SQL<IssueStatus>("SELECT s FROM IssueStatus s WHERE s.Name = ? FETCH ?", "Open", 1).First;
            var closed = Db.SQL<IssueStatus>("SELECT s FROM IssueStatus s WHERE s.Name = ? FETCH ?", "Closed", 1).First;
            List<ReportInformation> list = new List<ReportInformation>();
            foreach (var user in users) {
                Int64 count;
                WhereBuilder whereBuilder = new WhereBuilder();
                if (Setup.Status.Open) {
                    whereBuilder.Add("c.Issue.Status = ?", open);
                }
                else if (Setup.Status.Closed) {
                    whereBuilder.Add("c.Issue.Status = ?", closed);
                }
                if (Setup.Period.Last7Days) {
                    whereBuilder.Add("c.CreatedAt >= ?", DateTime.Today.AddDays(-7));
                }
                else if (Setup.Period.ThisYear) {
                    whereBuilder.Add("c.CreatedAt >= ?", new DateTime(DateTime.Today.Year, 1, 1));
                }
                whereBuilder.Add("c.Author = ?", user);
                count = Db.SQL<Int64>("SELECT COUNT(c) FROM Comment c WHERE " + whereBuilder.where, whereBuilder.whereParams.ToArray()).First;

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

    [Report_json.Setup.Status]
    partial class ReportSetupStatus : Partial {
        public event EventHandler Changed;

        protected void OnChanged() {
            if (this.Changed != null) {
                this.Changed(this, EventArgs.Empty);
            }
        }

        void Handle(Input.Open action) {
            Open = true;
            Closed = false;
            All = false;
            OnChanged();
        }
        void Handle(Input.Closed action) {
            Open = false;
            Closed = true;
            All = false;
            OnChanged();
        }
        void Handle(Input.All action) {
            Open = false;
            Closed = false;
            All = true;
            OnChanged();
        }
    }

    [Report_json.Setup.Period]
    partial class ReportSetupPeriod : Partial {
        public event EventHandler Changed;

        protected void OnChanged() {
            if (this.Changed != null) {
                this.Changed(this, EventArgs.Empty);
            }
        }

        void Handle(Input.Last7Days action) {
            Last7Days = true;
            ThisYear = false;
            AllTime = false;
            OnChanged();
        }
        void Handle(Input.ThisYear action) {
            Last7Days = false;
            ThisYear = true;
            AllTime = false;
            OnChanged();
        }
        void Handle(Input.AllTime action) {
            Last7Days = false;
            ThisYear = false;
            AllTime = true;
            OnChanged();
        }
    }
}
