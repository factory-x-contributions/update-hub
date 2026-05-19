// SPDX-FileCopyrightText: 2026 Fraunhofer-Institut für Produktionstechnik und Automatisierung IPA
// SPDX-FileCopyrightText: 2026 Hilscher Gesellschaft für Systemautomation mbH
// SPDX-FileCopyrightText: 2026 Siemens AG
//
// SPDX-License-Identifier: Apache-2.0

public class HttpProblemResponseException : Exception
{
  public HttpProblemResponseException(int statusCode, object? value = null)  =>
    (StatusCode, Value) = (statusCode, value);

  public int StatusCode { get; }

  public object? Value { get; }
}
