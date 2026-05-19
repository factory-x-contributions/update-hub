// SPDX-FileCopyrightText: 2026 Fraunhofer-Institut für Produktionstechnik und Automatisierung IPA
// SPDX-FileCopyrightText: 2026 Hilscher Gesellschaft für Systemautomation mbH
// SPDX-FileCopyrightText: 2026 Siemens AG
//
// SPDX-License-Identifier: Apache-2.0

using NuGet.Frameworks;
using Xunit;
using UpdateHub.Domain;

namespace UpdateHub.Domain.Test;

public partial class Tests
{

  //[SetUp]
  public void Setup()
  {
  }

  [Fact]
  public void AddAasServer()
  {
    // Arrange
    var aasServerRepository = new AasServerRepository();

    var aasServer = new AasServer
    {
      Name = "TestAASServer",
      IdLinkPrefix = "",
      Url = "http://localhost:8080",
      Auth = null
    };

    // Act
    aasServerRepository.AddAasServer(aasServer);

    // Assert
    Assert.NotNull(aasServerRepository.GetAll());
    Assert.Equal(aasServerRepository.GetAll().Count(),1);
    Assert.NotNull(aasServerRepository.GetAll().FirstOrDefault(c => c.Name == "TestAASServer"));
  }

  [Fact]
  public void GetAASServerWithIdLink()
  {
    // Arrange
    var aasServerRepository = new AasServerRepository();

    // Act
    aasServerRepository.AddAasServer(new AasServer
    {
      Name = "TestAASServer1",
      IdLinkPrefix = "https://idLink1/",
      Url = "http://localhost:8080",
      Auth = null
    });
    aasServerRepository.AddAasServer(new AasServer
    {
      Name = "TestAASServer2",
      IdLinkPrefix = "https://idLinkProvider2/",
      Url = "http://localhost:8080",
      Auth = null
    });

    // Assert
    Assert.NotNull(aasServerRepository.GetAll());
    Assert.Equal(aasServerRepository.GetAll().Count(),2);
    Assert.Null(aasServerRepository.GetByIdLink(""));
    Assert.NotNull(aasServerRepository.GetByIdLink("https://idLink1/"));
    Assert.Equal(aasServerRepository.GetByIdLink("https://idLink1/").Name, "TestAASServer1");
    Assert.Equal(aasServerRepository.GetByIdLink("https://idLink1/test").Name, "TestAASServer1");

    Assert.Equal(aasServerRepository.GetByIdLink("https://idLinkProvider2/test").Name, "TestAASServer2");
  }
}
