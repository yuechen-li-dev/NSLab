using System.Security.Cryptography;
using System.Text;

namespace NSLab.Core;

public static class RunId
{
    public static string FromScenarioJson(string scenarioJsonText)
    {
        var bytes = Encoding.UTF8.GetBytes(scenarioJsonText);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash.AsSpan(0, 8)).ToLowerInvariant();
    }
}
