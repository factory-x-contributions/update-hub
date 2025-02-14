public class HttpProblemResponseException : Exception
{
  public HttpProblemResponseException(int statusCode, object? value = null)  =>
    (StatusCode, Value) = (statusCode, value);

  public int StatusCode { get; }

  public object? Value { get; }
}
