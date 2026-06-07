using HtmlAgilityPack;
using System.Globalization;
using System.Text.RegularExpressions;
using Windows.UI.Popups;
using HtmlDocument = HtmlAgilityPack.HtmlDocument;
using System.Linq;
using System;

namespace AIAgentBot.Utilities
{
    public static class HtmlDataMasker
    {
        public static string MaskUserData(string html)
        {
            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            // 1. Remove obvious dangerous/sensitive tags if needed
            var removeTags = new[] { "script", "style", "meta", "link", "iframe" };
            foreach (var tag in removeTags)
            {
                var nodes = doc.DocumentNode.SelectNodes($"//{tag}");
                if (nodes != null)
                    foreach (var n in nodes)
                        n.Remove();
            }

            // 2. Attribute-level masking (IMPORTANT for images, links)
            MaskAttributes(doc);

            // 3. Element-level masking (emails, phones, names, dates)
            MaskElements(doc);

            return doc.DocumentNode.OuterHtml;
        }

        private static void MaskAttributes(HtmlDocument doc)
        {
            foreach (var node in doc.DocumentNode.Descendants().Where(n => n.NodeType == HtmlNodeType.Element))
            {
                if (node.Attributes["aria-label"] != null)
                {
                    var label = node.GetAttributeValue("aria-label", String.Empty);
                    string replacement = null;

                    if (IsEmail(label))
                        replacement = "{{email_address}}";
                    else if (IsPhone(label))
                        replacement = "{{phone_number}}";
                    else if (IsDate(label))
                        replacement = "{{date}}";
                    else if (IsLongText(label))
                        replacement = "{{long_text}}";

                    if (replacement != null)
                    {
                        node.SetAttributeValue("aria-label", replacement);
                    }
                }
                // IMG SRC masking
                if (node.Name == "img" && node.Attributes["src"] != null)
                {
                    var src = node.GetAttributeValue("src", String.Empty);

                    if (IsDataImage(src) || IsImageFile(src))
                        node.SetAttributeValue("src", "image_source");
                }

                // Generic src/href masking
                foreach (var attr in new[] { "href", "src" })
                {
                    var a = node.Attributes[attr];
                    if (a == null) continue;

                    if (IsDataImage(a.Value) || IsImageFile(a.Value) || IsUrl(a.Value))
                    {
                        node.SetAttributeValue(attr, $"{attr}_redacted");
                    }
                }
            }
        }

        private static void MaskElements(HtmlDocument doc)
        {
            var textNodes = doc.DocumentNode
                .Descendants()
                .Where(n => n.NodeType == HtmlNodeType.Text)
                .ToList();

            foreach (var textNode in textNodes)
            {
                var rawText = HtmlEntity.DeEntitize(textNode.InnerText).Trim();

                if (string.IsNullOrWhiteSpace(rawText))
                    continue;

                var normalized = Regex.Replace(rawText, @"\s+", " ").Trim();
                var compact = Regex.Replace(rawText, @"\s+", "").Trim();

                string replacement = null;

                if (IsEmail(compact))
                    replacement = "{{email_address}}";
                else if (IsPhone(normalized))
                    replacement = "{{phone_number}}";
                else if (IsDate(normalized))
                    replacement = "{{date}}";
                else if (IsLongText(normalized))
                    replacement = "{{long_text}}";

                if (replacement != null)
                {
                    textNode.InnerHtml = replacement;
                }
            }
        }

        private static bool IsEmail(string input)
        {
            return Regex.IsMatch(input,
                @"^[^@\s]+@[^@\s]+\.[^@\s]+$",
                RegexOptions.IgnoreCase);
        }

        private static bool IsPhone(string input)
        {
            // very broad international + Indian support
            return Regex.IsMatch(input,
                @"^(\+?\d[\d\s\-()]{7,}\d)$");
        }

        private static bool IsDate(string input)
        {
            return DateTime.TryParse(input,
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out _);
        }

        private static bool IsDataImage(string src)
        {
            return src.StartsWith("data:image", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsImageFile(string src)
        {
            return Regex.IsMatch(src, @"\.(png|jpg|jpeg|gif|webp|bmp)(\?.*)?$",
                RegexOptions.IgnoreCase);
        }

        private static bool IsUrl(string value)
        {
            return Uri.TryCreate(value, UriKind.Absolute, out _);
        }

        private static bool IsLongText(string value)
        {
            if (value.Length > 30)
                return true;

            return false;
        }

        // VERY conservative heuristic (avoids false positives)
        private static bool IsLikelyName(string text)
        {
            // Only 2–3 words, capitalized, no numbers/symbols
            if (text.Length < 3 || text.Length > 40)
                return false;

            if (Regex.IsMatch(text, @"\d"))
                return false;

            var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (words.Length > 3) return false;

            return words.All(w =>
                char.IsUpper(w[0]) &&
                Regex.IsMatch(w, @"^[A-Za-z\-]+$"));
        }
    }
}