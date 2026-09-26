using System;
using System.Globalization;

namespace YxGuides
{
    internal static class GuideShareCode
    {
        public static bool TryParse(string text, out string id, out int version)
        {
            id = "";
            version = 0;
            if (text == null) return false;
            string value = text.Trim();
            if (!value.StartsWith("YXG:", StringComparison.Ordinal)) return false;
            int at = value.LastIndexOf('@');
            if (at != 40 || at == value.Length - 1) return false;
            Guid parsed;
            if (!Guid.TryParseExact(value.Substring(4, 36), "D", out parsed)) return false;
            if (!int.TryParse(value.Substring(at + 1), NumberStyles.None, CultureInfo.InvariantCulture, out version) || version < 1) return false;
            id = parsed.ToString("D");
            return true;
        }
    }
}
