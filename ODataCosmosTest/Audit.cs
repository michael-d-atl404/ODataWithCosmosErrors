using Newtonsoft.Json;

namespace ODataCosmosTest
{
    public enum EntityType
    {
        Company = 1,
        Product = 2
    }

    public enum EntityChangeType
    {
        Create = 1,
        Update = 2,
        Delete = 3
    }

    public class Audit
    {
        [JsonProperty("id", Required = Required.Always)]
        public string Id { get; set; }

        public EntityType EntityType { get; set; }

        public string EntityId { get; set; }

        public EntityChangeType ChangeType { get; set; }

        public string User { get; set; }

        public DateTime Date { get; set; }

        public IEnumerable<AuditProp> Changes { get; set; }
    }
}
