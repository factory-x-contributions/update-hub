# AASQL-based Query - Endpoint Documentation

## Overview

The AASQL endpoint enables finding software updates via Asset Administration Shell Query Language (AASQL). The service uses nameplate attributes as search criteria to identify corresponding AAS shells and their update information.

## Endpoint Details

### URL
```
POST /aasql/update
```

### Content-Type
```
application/json
```

### Description
Executes an AASQL query to find software updates based on nameplate attributes.

## Request Format

### Request Body
```json
{
  "ManufacturerName": "string",
  "OrderCodeOfManufacturer": "string", 
  "ManufacturerProductType": "string"
}
```

### Parameter Description

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `ManufacturerName` | string | ✅ | Name of the manufacturer |
| `OrderCodeOfManufacturer` | string | ✅ | Order code of the manufacturer |
| `ManufacturerProductType` | string | ✅ | Product type of the manufacturer |

### Request Headers

| Header | Value | Optional | Description |
|--------|-------|----------|-------------|
| `Content-Type` | `application/json` | No | Format of the request body |
| `SKIP_PARSE_AAS` | `true`/`false` | Yes | Feature flag to skip AAS parsing |

## Response Format

### Successful Response (200 OK)
```json
[
  {
    "assetId": "string",
    "date": "string", 
    "version": "string",
    "installationUri": "string",
    "installationChecksum": "string",
    "softwareNameplateSubmodel": {},
    "pcnRecord": {}
  }
]
```

### Response Fields

| Field | Type | Description |
|-------|------|-------------|
| `assetId` | string | Unique ID of the AAS shell |
| `date` | string | Date of the update information |
| `version` | string | Software version |
| `installationUri` | string | URI for installation |
| `installationChecksum` | string | Installation checksum |
| `softwareNameplateSubmodel` | object | Complete AAS shell JSON data |
| `pcnRecord` | object | Product Change Notification record |

## Functionality

### 1. AASQL Query Construction
The service automatically creates an AASQL query based on the provided nameplate attributes:

```json
{
  "Query": {
    "$select": "id",
    "$condition": {
      "$and": [
        {
          "$eq": [
            { "$field": "$sme.ManufacturerName#value" },
            { "$strVal": "{ManufacturerName}" }
          ]
        },
        {
          "$eq": [
            { "$field": "$sme.OrderCodeOfManufacturer#value" },
            { "$strVal": "{OrderCodeOfManufacturer}" }
          ]
        },
        {
          "$eq": [
            { "$field": "$sme.ManufacturerProductType#value" },
            { "$strVal": "{ManufacturerProductType}" }
          ]
        },
        {
          "$eq": [
            { "$field": "$sm#idShort" },
            { "$strVal": "Nameplate" }
          ]
        }
      ]
    }
  }
}
```

### 2. Processing Steps

1. **Validation**: Verification of incoming parameters
2. **AAS Server Selection**: Using the first configured AAS server
3. **AASQL Shell Query**: Execution of the query for shell search
4. **Shell Data Retrieval**: Loading complete shell information
5. **Submodel Extraction**: Retrieval of PCN and SoftwareNameplate submodels
6. **Parsing**: Processing of PCN and nameplate data (optional)
7. **Response Construction**: Assembly of update information

### 3. Feature Flags

#### SKIP_PARSE_AAS
- **Header**: `SKIP_PARSE_AAS: true`
- **Purpose**: Skips detailed analysis of AAS submodels
- **Effect**: Returns raw shell data instead of parsed update information

## Error Handling

### HTTP Status Codes

| Status Code | Description | Possible Causes |
|-------------|-------------|----------------|
| `200` | Successful query | - |
| `400` | Bad Request | Invalid request body |
| `401` | Unauthorized | Authentication failed |
| `404` | Not Found | No AAS server configured |
| `422` | Unprocessable Entity | AASQL query failed |
| `500` | Internal Server Error | Unexpected server error |

### Example Error Responses

#### 404 - No AAS Server
```json
{
  "title": "Not Found",
  "status": 404,
  "detail": "No AAS Server found"
}
```

#### 422 - AASQL Query Fehler
```json
{
  "title": "Unprocessable Entity", 
  "status": 422,
  "detail": "Error while executing AASql shell query: 400 Bad Request"
}
```

## Examples

### Example Request
```bash
curl -X POST "http://localhost:8080/aasql/update" \
  -H "Content-Type: application/json" \
  -d '{
    "ManufacturerName": "Siemens AG",
    "OrderCodeOfManufacturer": "6ES7214-1AG40-0XB0",
    "ManufacturerProductType": "CPU 1214C"
  }'
```

### Example Response (Success)
```json
[
  {
    "assetId": "urn:example:asset:siemens:cpu1214c:123456",
    "date": "2026-01-28",
    "version": "4.5.2",
    "installationUri": "https://updates.example.com/firmware/cpu1214c-v4.5.2.zip",
    "installationChecksum": "sha256:abc123...",
    "softwareNameplateSubmodel": {
      "id": "urn:example:submodel:nameplate:123",
      "submodelElements": [...]
    },
    "pcnRecord": {
      "id": "urn:example:submodel:pcn:456", 
      "changeNotifications": [...]
    }
  }
]
```

### Example Request with Feature Flag
```bash
curl -X POST "http://localhost:8080/aasql/update" \
  -H "Content-Type: application/json" \
  -H "SKIP_PARSE_AAS: true" \
  -d '{
    "ManufacturerName": "Siemens AG",
    "OrderCodeOfManufacturer": "6ES7214-1AG40-0XB0", 
    "ManufacturerProductType": "CPU 1214C"
  }'
```

## Configuration

### AAS Server Configuration
The endpoint uses the AAS server configured in `config.yaml`. The first server in the list is used for AASQL requests. At development time, a locally started server was used since AASQL queries were only supported by this server version. https://github.com/eclipse-aaspe/server

```yaml
aasServers:
  - name: "Primary AAS Server"
    url: "https://aas-server.example.com"
    auth:
      type: "bearer"
      token: "your-auth-token"
```

### Authentication
Authentication is performed automatically based on the server configuration. Supported authentication types:
- Bearer Token
- API Key
- OAuth2
- Hilscher
- None

## Limitations

1. **Single Server**: Uses only the first configured AAS server
2. **Nameplate Dependency**: Requires nameplate submodels to be present
3. **Synchronous Processing**: No asynchronous processing for large datasets
4. **Memory Usage**: Loads complete shell data into memory


## Support & Development

For questions or issues with the AASQL endpoint:
1. Check the logs for detailed error messages
2. Validate the AAS server configuration
3. Test the AASQL query manually against the server
4. Verify the nameplate submodel structure

---

*Last update: January 2026*