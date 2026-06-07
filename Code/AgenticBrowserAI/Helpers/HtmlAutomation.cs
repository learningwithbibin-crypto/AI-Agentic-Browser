using HtmlAgilityPack;
using System;
using System.Threading.Tasks;

namespace AgenticBrowserAI.Helpers
{
    internal class HtmlAutomation
    {
        private HtmlDocument _document = new HtmlDocument();

        public HtmlAutomation(string html)
        {
            LoadHtml(html);
        }

        public void LoadHtml(string html)
        {
            _document.LoadHtml(html);
        }

        public async Task Wait(int millisecond)
        {
            await Task.Delay(millisecond);
        }       

        public void Loop(Action[]? steps = null)
        {
            if (steps == null || steps.Length == 0)
            {
                Console.WriteLine("Infinite loop started...");
                return;
            }

            foreach (var step in steps)
            {
                step.Invoke();
            }
        }

        public void Security(int millisecond)
        {

        }

        private (string key, string value) ParseLocator(string locator)
        {
            // FORMAT: [key="value"]

            locator = locator.Trim('[', ']');

            var parts = locator.Split('=');

            var key = parts[0];

            var value = parts[1]
                .Trim('"');

            return (key, value);
        }

        private bool NodeMatches(HtmlNode node, string key, string value)
        {
            return key.ToLower() switch
            {
                "id" => node.GetAttributeValue("id", "") == value,

                "name" => node.GetAttributeValue("name", "") == value,

                "role" => node.GetAttributeValue("role", "") == value,

                "placeholder" =>
                    node.GetAttributeValue("placeholder", "") == value,

                "aria-label" =>
                    node.GetAttributeValue("aria-label", "") == value,

                "value" =>
                    node.GetAttributeValue("value", "") == value,

                "text" =>
                    node.InnerText.Contains(value,
                        StringComparison.OrdinalIgnoreCase),

                _ => false
            };
        }
    }
}
