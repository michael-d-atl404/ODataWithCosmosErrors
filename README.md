# ODataWithCosmosErrors
OData with Cosmos errors

Created to debug errors I have been running into with OData on a Cosmos container.

This should run against a Cosmos database (you will have to update connection directly in \Controllers\AuditsController.cs as I did not take the time to do a real config in this dummy project). I ran against a local Cosmos Emulator (https://learn.microsoft.com/en-us/azure/cosmos-db/local-emulator).

Setup: Database = "Local", Container id = "Audits", Partition key = "/operationId"

Then for the indexing policy I updated to:

```
{
    "indexingMode": "consistent",
    "automatic": true,
    "includedPaths": [
        {
            "path": "/*"
        }
    ],
    "excludedPaths": [
        {
            "path": "/\"_etag\"/?"
        }
    ],
    "compositeIndexes": [
        [
            {
                "path": "/Date",
                "order": "descending"
            },
            {
                "path": "/id",
                "order": "descending"
            }
        ],
        [
            {
                "path": "/Date",
                "order": "ascending"
            },
            {
                "path": "/id",
                "order": "descending"
            }
        ]
    ]
}
```

The first 2 errors I was able to fix with Newtonsoft.Json default settings. Not ideal, but they seemed to help:

```
These errors only seemed to happen when the $select option is added to the base OData URL:

https://localhost:7253/odata/Audits?$select=id,user,date,changes

/* 
 * ERROR-1: ReferenceLoopHandling.Ignore solves this error:
 * 
   Newtonsoft.Json.JsonSerializationException: Self referencing loop detected for property 'DeclaringType' with type 'Microsoft.OData.Edm.EdmEntityType'. Path 'TypedProperty.SchemaElements[0].DeclaredKey[0]'.
    at Newtonsoft.Json.Serialization.JsonSerializerInternalWriter.CheckForCircularReference(JsonWriter writer, Object value, JsonProperty property, JsonContract contract, JsonContainerContract containerContract, JsonProperty containerProperty)
   ...
 * 
 * ERROR-2: then you have to add PreserveReferencesHandling.Objects to get past this next error:
 * 
    System.OutOfMemoryException: Insufficient memory to continue the execution of the program.
       at System.Text.StringBuilder.ExpandByABlock(Int32 minBlockCharCount)
       at System.Text.StringBuilder.Append(Char value, Int32 repeatCount)
       at System.Text.StringBuilder.Append(Char value)
       at System.IO.StringWriter.Write(Char value)
       at Newtonsoft.Json.JsonTextWriter.WriteEnd(JsonToken token)
       at Newtonsoft.Json.JsonWriter.AutoCompleteClose(JsonContainerType type)
       at Newtonsoft.Json.JsonWriter.WriteEnd(JsonContainerType type)
       at Newtonsoft.Json.JsonWriter.AutoCompleteAll()
       at Newtonsoft.Json.JsonConvert.SerializeObjectInternal(Object value, Type type, JsonSerializer jsonSerializer)
*/
JsonConvert.DefaultSettings = () => new JsonSerializerSettings
{
    ContractResolver = new CamelCasePropertyNamesContractResolver(),
    ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
    PreserveReferencesHandling = PreserveReferencesHandling.Objects,
};

```

The next error was apparently solved or bypassed by a code snippet I found from another user:

```
This error only seemed to happen when the $select option is added to the base OData URL:

https://localhost:7253/odata/Audits?$select=id,user,date,changes

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
    
```

The main error I am not yet able to resolve is:

```
https://localhost:7253/odata/Audits?$count=true&$select=id,user,date,changes&$orderby=date%20desc&$top=50

      Microsoft.Azure.Cosmos.Linq.DocumentQueryException: Expression with NodeType 'Conditional' is not supported., Windows/10.0.19044 cosmos-netstandard-sdk/3.29.4
         at Microsoft.Azure.Cosmos.Linq.ExpressionToSql.Translate(Expression inputExpression, TranslationContext context)
         at Microsoft.Azure.Cosmos.Linq.ExpressionToSql.VisitMethodCall(MethodCallExpression inputExpression, TranslationContext context)
         at Microsoft.Azure.Cosmos.Linq.ExpressionToSql.Translate(Expression inputExpression, TranslationContext context)
         at Microsoft.Azure.Cosmos.Linq.ExpressionToSql.VisitCollectionExpression(Expression expression, TranslationContext context, String parameterName)
         at Microsoft.Azure.Cosmos.Linq.ExpressionToSql.VisitCollectionExpression(Expression expression, ReadOnlyCollection`1 parameters, TranslationContext context)
         at Microsoft.Azure.Cosmos.Linq.ExpressionToSql.CreateSubquery(Expression expression, ReadOnlyCollection`1 parameters, TranslationContext context)
         at Microsoft.Azure.Cosmos.Linq.ExpressionToSql.VisitScalarExpression(Expression expression, ReadOnlyCollection`1 parameters, TranslationContext context)
         at Microsoft.Azure.Cosmos.Linq.ExpressionToSql.VisitConditional(ConditionalExpression inputExpression, TranslationContext context)
         at Microsoft.Azure.Cosmos.Linq.ExpressionToSql.VisitNonSubqueryScalarExpression(Expression expression, ReadOnlyCollection`1 parameters, TranslationContext context)
         at Microsoft.Azure.Cosmos.Linq.ExpressionToSql.VisitScalarExpression(Expression expression, ReadOnlyCollection`1 parameters, TranslationContext context)
         at Microsoft.Azure.Cosmos.Linq.ExpressionToSql.VisitMemberAssignment(MemberAssignment inputExpression, TranslationContext context)
         at Microsoft.Azure.Cosmos.Linq.ExpressionToSql.VisitBindingList(ReadOnlyCollection`1 inputExpressionList, TranslationContext context)
         at Microsoft.Azure.Cosmos.Linq.ExpressionToSql.VisitMemberInit(MemberInitExpression inputExpression, TranslationContext context)
         at Microsoft.Azure.Cosmos.Linq.ExpressionToSql.VisitNonSubqueryScalarExpression(Expression expression, ReadOnlyCollection`1 parameters, TranslationContext context)
         at Microsoft.Azure.Cosmos.Linq.ExpressionToSql.VisitScalarExpression(Expression expression, ReadOnlyCollection`1 parameters, TranslationContext context)
         at Microsoft.Azure.Cosmos.Linq.ExpressionToSql.VisitMemberAssignment(MemberAssignment inputExpression, TranslationContext context)
         at Microsoft.Azure.Cosmos.Linq.ExpressionToSql.VisitBindingList(ReadOnlyCollection`1 inputExpressionList, TranslationContext context)
         at Microsoft.Azure.Cosmos.Linq.ExpressionToSql.VisitMemberInit(MemberInitExpression inputExpression, TranslationContext context)
         at Microsoft.Azure.Cosmos.Linq.ExpressionToSql.VisitNonSubqueryScalarExpression(Expression expression, ReadOnlyCollection`1 parameters, TranslationContext context)
         at Microsoft.Azure.Cosmos.Linq.ExpressionToSql.VisitScalarExpression(Expression expression, ReadOnlyCollection`1 parameters, TranslationContext context)
         at Microsoft.Azure.Cosmos.Linq.ExpressionToSql.VisitMemberAssignment(MemberAssignment inputExpression, TranslationContext context)
         at Microsoft.Azure.Cosmos.Linq.ExpressionToSql.VisitBindingList(ReadOnlyCollection`1 inputExpressionList, TranslationContext context)
         at Microsoft.Azure.Cosmos.Linq.ExpressionToSql.VisitMemberInit(MemberInitExpression inputExpression, TranslationContext context)
         at Microsoft.Azure.Cosmos.Linq.ExpressionToSql.VisitNonSubqueryScalarExpression(Expression expression, ReadOnlyCollection`1 parameters, TranslationContext context)
         at Microsoft.Azure.Cosmos.Linq.ExpressionToSql.VisitScalarExpression(Expression expression, ReadOnlyCollection`1 parameters, TranslationContext context)
         at Microsoft.Azure.Cosmos.Linq.ExpressionToSql.VisitSelect(ReadOnlyCollection`1 arguments, TranslationContext context)
         at Microsoft.Azure.Cosmos.Linq.ExpressionToSql.VisitMethodCall(MethodCallExpression inputExpression, TranslationContext context)
         at Microsoft.Azure.Cosmos.Linq.ExpressionToSql.Translate(Expression inputExpression, TranslationContext context)
         at Microsoft.Azure.Cosmos.Linq.ExpressionToSql.VisitMethodCall(MethodCallExpression inputExpression, TranslationContext context)
         at Microsoft.Azure.Cosmos.Linq.ExpressionToSql.Translate(Expression inputExpression, TranslationContext context)
         at Microsoft.Azure.Cosmos.Linq.ExpressionToSql.TranslateQuery(Expression inputExpression, IDictionary`2 parameters, CosmosLinqSerializerOptions linqSerializerOptions)
         at Microsoft.Azure.Cosmos.Linq.SqlTranslator.TranslateQuery(Expression inputExpression, CosmosLinqSerializerOptions linqSerializerOptions, IDictionary`2 parameters)
         at Microsoft.Azure.Cosmos.Linq.CosmosLinqQuery`1.CreateFeedIterator(Boolean isContinuationExpected)
         at Microsoft.Azure.Cosmos.Linq.CosmosLinqQuery`1.GetEnumerator()+MoveNext()
         at System.Collections.Generic.List`1..ctor(IEnumerable`1 collection)
         at System.Linq.Enumerable.ToList[TSource](IEnumerable`1 source)
         at Microsoft.Azure.Cosmos.Linq.CosmosLinqQueryProvider.Execute[TResult](Expression expression)
         at System.Linq.Queryable.Count[TSource](IQueryable`1 source)
         at ODataCosmosTest.EnableCosmosQueryAttribute.<>c__DisplayClass0_0.<ApplyQuery>b__0() in C:\Users\michaeld\localProjects\ODataCosmosTest\ODataCosmosTest\EnableCosmosQueryAttribute.cs:line 26
         at Microsoft.AspNetCore.OData.Abstracts.ODataFeature.get_TotalCount()
         at Microsoft.AspNetCore.OData.Formatter.Serialization.ODataResourceSetSerializer.CreateResourceSet(IEnumerable resourceSetInstance, IEdmCollectionTypeReference resourceSetType, ODataSerializerContext writeContext)
         at Microsoft.AspNetCore.OData.Formatter.Serialization.ODataResourceSetSerializer.WriteResourceSetAsync(IEnumerable enumerable, IEdmTypeReference resourceSetType, ODataWriter writer, ODataSerializerContext writeContext)
         at Microsoft.AspNetCore.OData.Formatter.Serialization.ODataResourceSetSerializer.WriteObjectInlineAsync(Object graph, IEdmTypeReference expectedType, ODataWriter writer, ODataSerializerContext writeContext)
         at Microsoft.AspNetCore.OData.Formatter.Serialization.ODataResourceSetSerializer.WriteObjectAsync(Object graph, Type type, ODataMessageWriter messageWriter, ODataSerializerContext writeContext)
         at Microsoft.AspNetCore.OData.Formatter.ODataOutputFormatterHelper.WriteToStreamAsync(Type type, Object value, IEdmModel model, ODataVersion version, Uri baseAddress, MediaTypeHeaderValue contentType, HttpRequest request, IHeaderDictionary requestHeaders, IODataSerializerProvider serializerProvider)
         at Microsoft.AspNetCore.Mvc.Infrastructure.ResourceInvoker.<InvokeNextResultFilterAsync>g__Awaited|30_0[TFilter,TFilterAsync](ResourceInvoker invoker, Task lastTask, State next, Scope scope, Object state, Boolean isCompleted)
         at Microsoft.AspNetCore.Mvc.Infrastructure.ResourceInvoker.Rethrow(ResultExecutedContextSealed context)
         at Microsoft.AspNetCore.Mvc.Infrastructure.ResourceInvoker.ResultNext[TFilter,TFilterAsync](State& next, Scope& scope, Object& state, Boolean& isCompleted)
         at Microsoft.AspNetCore.Mvc.Infrastructure.ResourceInvoker.InvokeResultFilters()
      --- End of stack trace from previous location ---
         at Microsoft.AspNetCore.Mvc.Infrastructure.ResourceInvoker.<InvokeFilterPipelineAsync>g__Awaited|20_0(ResourceInvoker invoker, Task lastTask, State next, Scope scope, Object state, Boolean isCompleted)
         at Microsoft.AspNetCore.Mvc.Infrastructure.ResourceInvoker.<InvokeAsync>g__Awaited|17_0(ResourceInvoker invoker, Task task, IDisposable scope)
         at Microsoft.AspNetCore.Mvc.Infrastructure.ResourceInvoker.<InvokeAsync>g__Awaited|17_0(ResourceInvoker invoker, Task task, IDisposable scope)
         at Microsoft.AspNetCore.Routing.EndpointMiddleware.<Invoke>g__AwaitRequestTask|6_0(Endpoint endpoint, Task requestTask, ILogger logger)
         at Microsoft.AspNetCore.Authorization.AuthorizationMiddleware.Invoke(HttpContext context)
         at Swashbuckle.AspNetCore.SwaggerUI.SwaggerUIMiddleware.Invoke(HttpContext httpContext)
         at Swashbuckle.AspNetCore.Swagger.SwaggerMiddleware.Invoke(HttpContext httpContext, ISwaggerProvider swaggerProvider)
         at Microsoft.AspNetCore.Diagnostics.DeveloperExceptionPageMiddleware.Invoke(HttpContext context)
         at Microsoft.AspNetCore.Diagnostics.DeveloperExceptionPageMiddleware.Invoke(HttpContext context)
         at Microsoft.WebTools.BrowserLink.Net.BrowserLinkMiddleware.ExecuteWithFilterAsync(IHttpSocketAdapter injectScriptSocket, String requestId, HttpContext httpContext)
         at Microsoft.AspNetCore.Watch.BrowserRefresh.BrowserRefreshMiddleware.InvokeAsync(HttpContext context)
         at Microsoft.AspNetCore.Server.Kestrel.Core.Internal.Http.HttpProtocol.ProcessRequests[TContext](IHttpApplication`1 application)
```

Another error I am seeing with a different URL is also a little odd. I guess I can understand it but I am not sure why the IEnumerable "Changes" is not being treated as a navigatable property.

```
https://localhost:7253/odata/Audits?$count=true&$select=id,user,date&$expand=changes($select=prop,old,new)&$orderby=date%20desc&$top=50

{
  "error":
  {
    "code":"",
    "message":"The query specified in the URI is not valid. Property 'changes' on type 'ODataCosmosTest.Audit' is not a navigation property or complex property. Only navigation properties can be expanded.",
    "details":[],
    "innererror":
    {
      "message":"Property 'changes' on type 'ODataCosmosTest.Audit' is not a navigation property or complex property. Only navigation properties can be expanded.",
      "type":"Microsoft.OData.ODataException",
      "stacktrace":"   at Microsoft.OData.UriParser.SelectExpandBinder.ParseComplexTypesBeforeNavigation(IEdmStructuralProperty edmProperty, PathSegmentToken& currentToken, List`1 pathSoFar)
        at Microsoft.OData.UriParser.SelectExpandBinder.GenerateExpandItem(ExpandTermToken tokenIn)
        at System.Linq.Enumerable.SelectEnumerableIterator`2.MoveNext()
        at System.Linq.Enumerable.WhereEnumerableIterator`1.MoveNext()
        at System.Collections.Generic.List`1.InsertRange(Int32 index, IEnumerable`1 collection)
        at Microsoft.OData.UriParser.SelectExpandBinder.Bind(ExpandToken expandToken, SelectToken selectToken)
        at Microsoft.OData.UriParser.SelectExpandSemanticBinder.Bind(ODataPathInfo odataPathInfo, ExpandToken expandToken, SelectToken selectToken, ODataUriParserConfiguration configuration, BindingState state)
        at Microsoft.OData.UriParser.ODataQueryOptionParser.ParseSelectAndExpandImplementation(String select, String expand, ODataUriParserConfiguration configuration, ODataPathInfo odataPathInfo)
        at Microsoft.OData.UriParser.ODataQueryOptionParser.ParseSelectAndExpand()
        at Microsoft.AspNetCore.OData.Query.Validator.SelectExpandQueryValidator.Validate(SelectExpandQueryOption selectExpandQueryOption, ODataValidationSettings validationSettings)
        at Microsoft.AspNetCore.OData.Query.Validator.ODataQueryValidator.Validate(ODataQueryOptions options, ODataValidationSettings validationSettings)
        at Microsoft.AspNetCore.OData.Query.EnableQueryAttribute.ValidateQuery(HttpRequest request, ODataQueryOptions queryOptions)
        at Microsoft.AspNetCore.OData.Query.EnableQueryAttribute.OnActionExecuting(ActionExecutingContext actionExecutingContext)"
    }
  }
}
```
