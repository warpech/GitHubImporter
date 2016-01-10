using Starcounter;
using System;

namespace GitHubImporter {
    partial class ReportSetup : Json {
        public void BuildWhere (QueryBuilder whereBuilder) {
            var open = Db.SQL<IssueStatus>("SELECT s FROM IssueStatus s WHERE s.Name = ? FETCH ?", "Open", 1).First;
            var closed = Db.SQL<IssueStatus>("SELECT s FROM IssueStatus s WHERE s.Name = ? FETCH ?", "Closed", 1).First;

            if (Status.Open) {
                whereBuilder.Where("c.Issue.Status = ?", open);
            }
            else if (Status.Closed) {
                whereBuilder.Where("c.Issue.Status = ?", closed);
            }

            if (Period.Last7Days) {
                whereBuilder.Where("c.CreatedAt >= ?", DateTime.Today.AddDays(-7));
            }
            else if (Period.ThisMonth) {
                whereBuilder.Where("c.CreatedAt >= ?", new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1));
            }
            else if (Period.LastMonth) {
                var from = DateTime.Today.AddMonths(-1);
                whereBuilder.Where("c.CreatedAt >= ?", new DateTime(from.Year, from.Month, 1));
                whereBuilder.Where("c.CreatedAt < ?", new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1));
            }
            else if (Period.ThisYear) {
                whereBuilder.Where("c.CreatedAt >= ?", new DateTime(DateTime.Today.Year, 1, 1));
            }
            else if (Period.LastYear) {
                whereBuilder.Where("c.CreatedAt >= ?", new DateTime(DateTime.Today.Year - 1, 1, 1));
                whereBuilder.Where("c.CreatedAt < ?", new DateTime(DateTime.Today.Year, 1, 1));
            }
        }
    }

    [ReportSetup_json.Status]
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

    [ReportSetup_json.Period]
    partial class ReportSetupPeriod : Partial {
        public event EventHandler Changed;

        protected void OnChanged() {
            if (this.Changed != null) {
                this.Changed(this, EventArgs.Empty);
            }
        }

        void ResetPeriod() {
            Last7Days = false;
            ThisMonth = false;
            LastMonth = false;
            ThisYear = false;
            LastYear = false;
            AllTime = false;
        }

        void Handle(Input.Last7Days action) {
            ResetPeriod();
            Last7Days = true;
            OnChanged();
        }
        void Handle(Input.ThisMonth action) {
            ResetPeriod();
            ThisMonth = true;
            OnChanged();
        }
        void Handle(Input.LastMonth action) {
            ResetPeriod();
            LastMonth = true;
            OnChanged();
        }
        void Handle(Input.ThisYear action) {
            ResetPeriod();
            ThisYear = true;
            OnChanged();
        }
        void Handle(Input.LastYear action) {
            ResetPeriod();
            LastYear = true;
            OnChanged();
        }
        void Handle(Input.AllTime action) {
            ResetPeriod();
            AllTime = true;
            OnChanged();
        }
    }
}
