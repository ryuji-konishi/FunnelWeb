using System;
using System.Linq;
using FunnelWeb.Model;
using FunnelWeb.Repositories.Projections;
using NHibernate;
using NHibernate.Criterion;
using NHibernate.Criterion.Lambda;
using NHibernate.Transform;

namespace FunnelWeb.Repositories.Queries
{
    public class GetFullEntriesQuery : IPagedQuery<EntryRevision>
    {
        private readonly bool includeComments;
        private readonly EntriesSortColumn sortColumn;
        private readonly bool asc;

        public GetFullEntriesQuery(bool includeComments = false, EntriesSortColumn sortColumn = EntriesSortColumn.Published, bool asc = false)
        {
            this.includeComments = includeComments;
            this.sortColumn = sortColumn;
            this.asc = asc;
        }

        public PagedResult<EntryRevision> Execute(ISession session, int skip, int take)
        {
            var total = session
                .QueryOver<Entry>()
                .ToRowCountQuery()
                .FutureValue<int>();

            var entries = Query(session)
                .Skip(skip)
                .Take(take)
                .Future<EntryRevision>();

            if (includeComments)
            {
                //This fetches all comments with 1 query, and without bringing back duplicated entry data which would happen in a join
                var comments =
                    session.QueryOver<Comment>()
                        .WithSubquery.WhereProperty(c => c.Entry.Id).In(QueryOver.Of<Entry>().Select(e => e.Id).Skip(skip).Take(take))
                        .Future()
                        .GroupBy(k=>k.Entry.Id)
                        .ToDictionary(k=>k.Key);

                entries = entries.Select(e =>
                                             {
                                                 e.Comments = comments[e.Id].ToList();
                                                 return e;
                                             });
            }

            return new PagedResult<EntryRevision>(entries.ToList(), total.Value, skip, take);
        }

        protected IQueryOver<Entry, Entry> Query(ISession session)
        {
            var entryRevisionAlias = new EntryRevision();

            var entries = session
                .QueryOver<Entry>()
                .SelectList(EntryRevisionProjections.FromEntry(entryRevisionAlias))
                .TransformUsing(Transformers.AliasToBean<EntryRevision>());

            IQueryOverOrderBuilder<Entry, Entry> orderBy = null;

            switch (sortColumn)
            {
                case EntriesSortColumn.Slug:
                    orderBy = entries.OrderBy(e => e.Name);
                    break;
                case EntriesSortColumn.Title:
                    orderBy = entries.OrderBy(e => e.Title);
                    break;
                case EntriesSortColumn.Comments:
                    orderBy = entries.OrderBy(e => e.CommentCount);
                    break;
                case EntriesSortColumn.Published:
                    orderBy = entries.OrderBy(e => e.Published);
                    break;
            }
            if (orderBy != null)
                entries = asc ? orderBy.Asc : orderBy.Desc;

            return entries;
        }
    }
}