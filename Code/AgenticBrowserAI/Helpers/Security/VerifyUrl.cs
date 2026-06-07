using System;

namespace AgenticBrowserAI.Helpers.Security
{
    internal static class VerifyUrl
    {
        public static SecurityResult? Check(string url)
        {
            var result = CallApi(url);

            if (result != null)
            {
                FileLogger.Log($"Url: {result.Url}\r\nReason: {(result.IsMalicious ? "This url is malicious." : "Url seems fine.")} {result.Description}", LogLevel.Info, true);
            }

            return result;
        }

        private static SecurityResult? CallApi(string url)
        {
            if (String.IsNullOrWhiteSpace(url))
            {
                return null;
            }

            var result = Validate(url);
            result.Url = url;

            return result;
        }

        private static SecurityResult Validate(string url)
        {
            SecurityResult securityResult = new SecurityResult();

            if (url.Contains(".exe", StringComparison.InvariantCultureIgnoreCase))
            {
                securityResult.IsMalicious = true;
                securityResult.Description = "Url downloads executable file that could be harmful to your device.";
            }
            else if (url.Contains("tracking", StringComparison.InvariantCultureIgnoreCase))
            {
                securityResult.IsMalicious = true;
                securityResult.Description = "This url is able to track your activities.";
            }

            return securityResult;
        }
    }

    internal class SecurityResult
    {
        internal string Url { get; set; }
        internal bool IsMalicious { get; set; }
        internal string Description { get; set; }
    }
}
