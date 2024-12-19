GET http://localhost:5292/version
HTTP 200
[Captures]
in: body

# https://smart.festo.com/asset/type/oc/CMMT-AS-C2-3A-MP-S1
GET http://localhost:5292/update/https%3A%2F%2Fsmart.festo.com%2Fasset%2Ftype%2Foc%2FCMMT-AS-C2-3A-MP-S1
HTTP 200
[Captures]
in: body

# https://pid.sick.com/1098146
GET http://localhost:5292/update/https%3A%2F%2Fpid.sick.com%2F1098146
HTTP 200

#
GET http://localhost:5292/update/test
HTTP 404
