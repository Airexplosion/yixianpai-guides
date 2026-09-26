using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Yx.Shared
{
    /// <summary>极简 JSON（ILRuntime 友好）。对象→Dictionary&lt;string, object&gt;，数组→List&lt;object&gt;，数字→double。</summary>
    public static class Json
    {
        public const char Backslash = (char)92;
        const char Quote = (char)34;

        public static object Parse(string text)
        {
            if (text == null) throw new FormatException("JSON 为空");
            var p = new JsonParser(text);
            p.SkipWhitespace();
            object value = p.ReadValue();
            p.SkipWhitespace();
            if (!p.AtEnd) throw new FormatException("JSON 末尾有多余字符（位置 " + p.Position.ToString(CultureInfo.InvariantCulture) + "）");
            return value;
        }

        public static Dictionary<string, object> ParseObject(string text)
        {
            var d = Parse(text) as Dictionary<string, object>;
            if (d == null) throw new FormatException("JSON 顶层不是对象");
            return d;
        }

        public static string Serialize(object value)
        {
            var sb = new StringBuilder();
            Write(sb, value);
            return sb.ToString();
        }

        public static string GetString(Dictionary<string, object> d, string key, string fallback)
        {
            object v;
            if (d != null && d.TryGetValue(key, out v))
            {
                var s = v as string;
                if (s != null) return s;
            }
            return fallback;
        }

        public static bool GetBool(Dictionary<string, object> d, string key, bool fallback)
        {
            object v;
            if (d != null && d.TryGetValue(key, out v) && v is bool) return (bool)v;
            return fallback;
        }

        public static double GetNumber(Dictionary<string, object> d, string key, double fallback)
        {
            object v;
            if (d != null && d.TryGetValue(key, out v) && v is double) return (double)v;
            return fallback;
        }

        public static Dictionary<string, object> GetObject(Dictionary<string, object> d, string key)
        {
            object v;
            if (d != null && d.TryGetValue(key, out v)) return v as Dictionary<string, object>;
            return null;
        }

        public static List<object> GetArray(Dictionary<string, object> d, string key)
        {
            object v;
            if (d != null && d.TryGetValue(key, out v)) return v as List<object>;
            return null;
        }

        static void Write(StringBuilder sb, object value)
        {
            if (value == null) { sb.Append("null"); return; }
            var s = value as string;
            if (s != null) { WriteString(sb, s); return; }
            if (value is bool) { sb.Append((bool)value ? "true" : "false"); return; }
            if (value is int) { sb.Append(((int)value).ToString(CultureInfo.InvariantCulture)); return; }
            if (value is long) { sb.Append(((long)value).ToString(CultureInfo.InvariantCulture)); return; }
            if (value is float) { WriteFloat(sb, (float)value); return; }
            if (value is double) { WriteDouble(sb, (double)value); return; }
            var dict = value as IDictionary;
            if (dict != null)
            {
                sb.Append('{');
                bool first = true;
                foreach (DictionaryEntry e in dict)
                {
                    if (!first) sb.Append(',');
                    first = false;
                    WriteString(sb, Convert.ToString(e.Key, CultureInfo.InvariantCulture));
                    sb.Append(':');
                    Write(sb, e.Value);
                }
                sb.Append('}');
                return;
            }
            var list = value as IEnumerable;
            if (list != null)
            {
                sb.Append('[');
                bool first = true;
                foreach (object item in list)
                {
                    if (!first) sb.Append(',');
                    first = false;
                    Write(sb, item);
                }
                sb.Append(']');
                return;
            }
            WriteString(sb, Convert.ToString(value, CultureInfo.InvariantCulture));
        }

        static void WriteDouble(StringBuilder sb, double d)
        {
            if (double.IsNaN(d) || double.IsInfinity(d)) { sb.Append("null"); return; }
            sb.Append(d.ToString("R", CultureInfo.InvariantCulture));
        }

        static void WriteFloat(StringBuilder sb, float f)
        {
            if (float.IsNaN(f) || float.IsInfinity(f)) { sb.Append("null"); return; }
            sb.Append(f.ToString("R", CultureInfo.InvariantCulture));
        }

        static void WriteString(StringBuilder sb, string s)
        {
            sb.Append(Quote);
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (c == Quote) { sb.Append(Backslash).Append(Quote); }
                else if (c == Backslash) { sb.Append(Backslash).Append(Backslash); }
                else if (c == (char)10) { sb.Append(Backslash).Append('n'); }
                else if (c == (char)13) { sb.Append(Backslash).Append('r'); }
                else if (c == (char)9) { sb.Append(Backslash).Append('t'); }
                else if (c < (char)32)
                {
                    sb.Append(Backslash).Append('u');
                    sb.Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                }
                else sb.Append(c);
            }
            sb.Append(Quote);
        }
    }
}
