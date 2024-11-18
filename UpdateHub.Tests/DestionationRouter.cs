using Xunit;
using UpdateHub.DestinationRouter;

namespace UpdateHub
  .UnitTests.DestionationRouter
{

  public class Tests
  {
    [Fact]
    public void Test1()
    {
      var router = new DestinationRouter.DestinationRouter();
        Assert.Null(router.Probe(""));

    }
  }
}
