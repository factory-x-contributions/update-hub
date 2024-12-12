using System.Text;

namespace UpdateHub.Helper;

// TODO: Dotnet 9.x provides a built-in Base64Url implementation. Should be migrated
public static class Base64UrlOwnImplementation
{
  public static string Encode(string text)
  {

    return Convert.ToBase64String(Encoding.UTF8.GetBytes(text)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
  }

  public static string Decode(string text)
  {
    text = text.Replace('_', '/').Replace('-', '+');
    switch (text.Length % 4)
    {
      case 2:
        text += "==";
        break;
      case 3:
        text += "=";
        break;
    }
    return Encoding.UTF8.GetString(Convert.FromBase64String(text.Normalize()));
  }
}
