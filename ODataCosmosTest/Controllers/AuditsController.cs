using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OData.Query;
using Microsoft.Azure.Cosmos;
using System.ComponentModel;

namespace ODataCosmosTest.Controllers
{
    public class AuditsController : ControllerBase
    {
        [HttpGet, EnableCosmosQuery(
            PageSize = 50, 
            EnsureStableOrdering = false,
            HandleNullPropagation = Microsoft.AspNetCore.OData.Query.HandleNullPropagationOption.False, 
            HandleReferenceNavigationPropertyExpandFilter = true)]
        public IActionResult Get()
        {
            var database = new CosmosClient(
                "AccountEndpoint=https://localhost:8081/;AccountKey=C2y6yDjf5/R+ob0N8A7Cgv30VRDJIWEHLM+4QDU5DE2nQ9nDuVTqobD4b8mGGyPMbIZnqyMsEcaGQy67XIw/Jw==",
                new CosmosClientOptions() { ConnectionMode = ConnectionMode.Gateway })
                        .GetDatabase("Local");

            var container = database.GetContainer("Audits");

            return Ok(container.GetItemLinqQueryable<Audit>(allowSynchronousQueryExecution: true).Where(a => a.Id == "1234:Company"));
        }
    }
}
