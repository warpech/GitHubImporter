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
            var open = Db.SQL<IssueStatus>("SELECT s FROM IssueStatus s WHERE s.Name = ? FETCH ?", "Open", 1).First;
            var closed = Db.SQL<IssueStatus>("SELECT s FROM IssueStatus s WHERE s.Name = ? FETCH ?", "Closed", 1).First;
            List<ReportInformation> list = new List<ReportInformation>();
            foreach (var user in users) {
                Int64 count;
                if (Setup.Status.Open) {
                    count = Db.SQL<Int64>("SELECT COUNT(c) FROM Comment c WHERE c.Issue.Status = ? AND c.Author = ?", open, user).First;
                }
                else if (Setup.Status.Closed) {
                    count = Db.SQL<Int64>("SELECT COUNT(c) FROM Comment c WHERE c.Issue.Status = ? AND c.Author = ?", closed, user).First;
                }
                else {
                    count = Db.SQL<Int64>("SELECT COUNT(c) FROM Comment c WHERE c.Author = ?", user).First;
                }

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
            Closed = false;
            Open = false;
            All = true;
            OnChanged();
        }
    }
}
