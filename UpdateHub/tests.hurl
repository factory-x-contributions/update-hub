GET http://localhost:5292/version
HTTP 200
[Asserts]
jsonpath "$.major" != null
jsonpath "$.minor" != null
jsonpath "$.patch" != null
jsonpath "$.hash" != null
[Captures]
in: body

# No IDlink matches AAS Server
GET http://localhost:5292/update/test
HTTP 404

#
# https://aas.test.com
#
# https://aas.test.com/asset/type/test
GET http://localhost:5292/v1/update/https%3A%2F%2Faas.test.com%2Fasset%2Ftype%2Ftest
# Temporary, since the Aaas Lib does only support amd64
SKIP_PARSE_AAS: true
HTTP 200
[Asserts]
status == 200
jsonpath "$.productChangeNotifications" count != 0
jsonpath "$.softwareNamePlates" count != 0
[Captures]
in: body
