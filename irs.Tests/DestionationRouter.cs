using Xunit;
using irs.DestinationRouter;

namespace irs.UnitTests.DestionationRouter
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
