using Microsoft.AspNetCore.OData;
using Microsoft.AspNetCore.OData.NewtonsoftJson;
using Microsoft.OData.ModelBuilder;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;
using ODataCosmosTest;

var builder = WebApplication.CreateBuilder(args);

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

// Add services to the container.

builder.Services
    .AddControllers()
    .AddOData(o =>
    {
        // Define OData entities
        var builder = new ODataConventionModelBuilder();
        builder.EnableLowerCamelCase();

        builder.EntitySet<WeatherForecast>("WeatherForecasts");
        builder.EntitySet<Audit>("Audits");

        // Add the root OData Model Set
        o.AddRouteComponents("odata", builder.GetEdmModel());

        // Activate the OData options
        o.Select().Filter().Expand().OrderBy().Count().SetMaxTop(100);

        // Return all data as UTC as that is how it should be stored
        o.TimeZone = TimeZoneInfo.Utc;
    });

    //.AddNewtonsoftJson(o =>
    //{
    //    o.SerializerSettings.ContractResolver = new CamelCasePropertyNamesContractResolver();
    //    o.SerializerSettings.Converters.Add(new StringEnumConverter());
    //    o.SerializerSettings.ReferenceLoopHandling = ReferenceLoopHandling.Ignore;
    //    o.SerializerSettings.PreserveReferencesHandling = PreserveReferencesHandling.Objects;
    //});

    //.AddODataNewtonsoftJson();

// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
