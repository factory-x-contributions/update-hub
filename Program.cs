
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c => c.SwaggerDoc("v1", new OpenApiInfo
{
  Title = "Information Requesting Service", Version = "v1",
  Description = "Example service to resolve an IDLink to PCNs",
}));

var app = builder.Build();


    app.UseSwagger();
    app.UseSwaggerUI();


//app.UseHttpsRedirection();

var summaries = new[]
{
    "PCN-1"
};

app.MapGet("/update/{IdLink}", (string IdLink) =>
    {
      if (string.IsNullOrEmpty(IdLink) || IdLink.ToUpper().Contains("ERROR"))
      {
        return Results.Problem("IdLink cannot be empty", statusCode: StatusCodes.Status400BadRequest);
      }
      //TODO: Some usefull
        var productChangeNofication = Enumerable.Range(1, 5).Select(index =>
                new ProductChangeNofication
                (
                    summaries[Random.Shared.Next(summaries.Length)],
                    IdLink
                ))
            .ToArray();
        return Results.Ok(productChangeNofication);
    })
    .WithName("update")
    .Produces<ProductChangeNofication[]>(StatusCodes.Status200OK)
    .Produces(StatusCodes.Status400BadRequest)
    .WithOpenApi();

app.Run();

record ProductChangeNofication(string? Summary, string IdLink)
{

}
