using Microsoft.AspNetCore.OData.Extensions;
using Microsoft.AspNetCore.OData.Query;
using System.Collections;

namespace ODataCosmosTest
{
    /*
     * ERROR-3 added this in to resolve this error:

        Microsoft.Azure.Cosmos.CosmosException : Response status code does not indicate success: BadRequest (400); Substatus: 0; ActivityId: ; Reason: ({"errors":[{"severity":"Error","location":{"start":0,"end":1150768},"code":"SC3020","message":"The SQL query text exceeded the maximum limit of 262144 characters."}]});
     ---> Microsoft.Azure.Cosmos.Query.Core.Exceptions.ExpectedQueryPartitionProviderException: {"errors":[{"severity":"Error","location":{"start":0,"end":1150768},"code":"SC3020","message":"The SQL query text exceeded the maximum limit of 262144 characters."}]}
    */

    public class EnableCosmosQueryAttribute : EnableQueryAttribute
    {
        /// <summary>
        /// https://github.com/OData/WebApi/issues/2118
        /// </summary>
        /// <param name="queryable"></param>
        /// <param name="queryOptions"></param>
        /// <returns></returns>
        public override IQueryable ApplyQuery(IQueryable queryable, ODataQueryOptions queryOptions)
        {
            if (queryOptions.Count?.Value == true)
            {
                queryOptions.Request.ODataFeature().TotalCountFunc = () => (long)Queryable.Count(queryOptions.ApplyTo(queryable, AllowedQueryOptions.Top | AllowedQueryOptions.Skip | AllowedQueryOptions.OrderBy) as IQueryable<object>);
            }

            var cosmosQuery = queryOptions.ApplyTo(
                queryable,
                new ODataQuerySettings()
                {
                    EnableConstantParameterization = this.EnableConstantParameterization,
                    EnableCorrelatedSubqueryBuffering = this.EnableCorrelatedSubqueryBuffering,
                    EnsureStableOrdering = this.EnsureStableOrdering,
                    HandleNullPropagation = this.HandleNullPropagation,
                    HandleReferenceNavigationPropertyExpandFilter = this.HandleReferenceNavigationPropertyExpandFilter,
                    PageSize = (this.PageSize > 0 ? this.PageSize : (int?)null),
                },
                AllowedQueryOptions.Select | AllowedQueryOptions.Expand);

            var genericToList = typeof(Enumerable).GetMethod("ToList")
                .MakeGenericMethod(new Type[] { cosmosQuery.ElementType });
            var queryResult = (IList)genericToList.Invoke(null, new[] { cosmosQuery });

            return queryOptions.ApplyTo(queryResult.AsQueryable(), AllowedQueryOptions.All ^ (AllowedQueryOptions.Select | AllowedQueryOptions.Expand));
        }
    }
}
