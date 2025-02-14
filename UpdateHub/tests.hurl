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
# https://aas.festo.com
#
# https://smart.festo.com/asset/type/oc/CMMT-AS-C2-3A-MP-S1
GET http://localhost:5292/v1/update/https%3A%2F%2Fsmart.festo.com%2Fasset%2Ftype%2Foc%2FCMMT-AS-C2-3A-MP-S1
# Temporary, since the Aaas Lib does only support amd64
SKIP_PARSE_AAS: true
HTTP 200
[Asserts]
status == 200
jsonpath "$.productChangeNotifications" count != 0
jsonpath "$.softwareNamePlates" count != 0
[Captures]
in: body

# https://smart.festo.com/asset/type/oc/CPX-E-CEC-C1-PN
GET http://localhost:5292/v1/update/https%3A%2F%2Fsmart.festo.com%2Fasset%2Ftype%2Foc%2FCPX-E-CEC-C1-PN
# Temporary, since the Aaas Lib does only support amd64
SKIP_PARSE_AAS: true
HTTP 200
[Asserts]
status == 200
jsonpath "$.productChangeNotifications" count != 0
jsonpath "$.softwareNamePlates" count != 0
[Captures]
in: body

# https://smart.festo.com/asset/type/oc/CMMT-ST-C8-1C-MP-S0
GET http://localhost:5292/v1/update/https%3A%2F%2Fsmart.festo.com%2Fasset%2Ftype%2Foc%2FCMMT-ST-C8-1C-MP-S0
# Temporary, since the Aaas Lib does only support amd64
SKIP_PARSE_AAS: true
HTTP 200
[Asserts]
status == 200
jsonpath "$.productChangeNotifications" count != 0
jsonpath "$.softwareNamePlates" count != 0
[Captures]
in: body

#
# https://aasx.dev-cloud.sick.com/swagger/index.html
#
# https://pid.sick.com/1098146
GET http://localhost:5292/v1/update/pid.sick.com%2F1098146
# Temporary, since the Aaas Lib does only support amd64
SKIP_PARSE_AAS: true
HTTP 200
[Asserts]
status == 200
jsonpath "$.productChangeNotifications" count != 0
jsonpath "$.softwareNamePlates" count != 0
[Captures]
in: body

#
# https://hilscher-aas.netiot.com/
#
# https://i.hilscher.com/00000000wlp
GET http://localhost:5292/v1/update/https%3A%2F%2Fi.hilscher.com%2F00000000wlp
# Temporary, since the Aaas Lib does only support amd64
SKIP_PARSE_AAS: true
HTTP 200
[Asserts]
status == 200
jsonpath "$.productChangeNotifications" count != 0
jsonpath "$.softwareNamePlates" count != 0
[Captures]
in: body

