# Information Requesting Service

Service requesting information from downstream services for a given asset.

```mermaid
C4Context
  Boundary(b_cdm, "Common Device Management") {
    Person(customer, "Operator", "", "Operator")
    System(irs, "Information Requesting Service")

    Rel_Right(customer, irs, "Uses")
  }

  Boundary(b_supplier, "Suppliers") {
    System_Ext(ips, "Information Providing Service Supplier 1", "AAS", "Provides product change Notifications (PCN)")
    System_Ext(ip2, "Information Providing Service Supplier 2", "AAS", "Provides product change Notifications (PCN)")
    Rel_Up(irs, ips, "asks for PCNs")
    Rel_Down(irs, ip2, "asks for PCNs")
  }

  Rel_Up(irs, ips, "asks for PCNs")
  Rel_Down(irs, ip2, "asks for PCNs")
```

## Build && Run && Test

```bash
# Run the service locally and serving the endpoint on
# http://localhost:8080/swagger/
$ cd irs/ dotnet run
```
