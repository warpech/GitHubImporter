using Starcounter;

namespace GitHubImporter {
    partial class CommentsList : Page {
        public void RefreshReport() {
            QueryBuilder queryBuilder = new QueryBuilder();
            Setup.BuildWhere(queryBuilder);
            queryBuilder.Where("c.Author.Name = ?", "warpech");
            queryBuilder.OrderBy("c.CreatedAt DESC");
            queryBuilder.Fetch(100);

            var results = Db.SQL<Comment>("SELECT c FROM Comment c WHERE " + queryBuilder.Query, queryBuilder.Params);

            Title = "Comment browser";
            Results.Clear();
            Results.Data = results;
        }
    }
}
