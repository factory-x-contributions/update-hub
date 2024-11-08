using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace irs.Controllers;

[ApiController]
[Route("[controller]")]
public class UpdateController : ControllerBase
{
  private readonly ILogger<UpdateController> _logger;

  public UpdateController(ILogger<UpdateController> logger)
  {
    _logger = logger;
  }


  [HttpGet("{IdLink}")]
  [ProducesResponseType<ProductChangeNofication>(StatusCodes.Status200OK)]
  [ProducesResponseType(StatusCodes.Status405MethodNotAllowed)]
  [Produces("application/json")]
  public async Task<IResult> GetProductChangeNotification(string IdLink)
  {
    return Results.Problem("Not implemented, yet.", statusCode: StatusCodes.Status405MethodNotAllowed);
    var productChangeNofication = Enumerable.Range(1, 5).Select(index =>
        new ProductChangeNofication
        (
          "PCN-" + index,
          IdLink
        ))
      .ToArray();
    return Results.Ok(productChangeNofication);
  }

  record ProductChangeNofication(string? Summary, string IdLink) { }
}
