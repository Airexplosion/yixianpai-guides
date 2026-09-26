using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Yx.Shared
{
    internal sealed class JsonParser
    {
        readonly string _s;
        int _i;

        public JsonParser(string s) { _s = s; _i = 0; }

        public int Position { get { return _i; } }
        public bool AtEnd { get { return _i >= _s.Length; } }

        public void SkipWhitespace()
        {
            while (_i < _s.Length)
            {
                char c = _s[_i];
                if (c == ' ' || c == (char)9 || c == (char)10 || c == (char)13) _i++;
                else break;
            }
        }

        public object ReadValue()
        {
            if (AtEnd) throw Error("意外的结尾");
            char c = _s[_i];
            if (c == '{') return ReadObject();
            if (c == '[') return ReadArray();
            if (c == (char)34) return ReadString();
            if (c == 't') { Expect("true"); return true; }
            if (c == 'f') { Expect("false"); return false; }
            if (c == 'n') { Expect("null"); return null; }
            if (c == '-' || (c >= '0' && c <= '9')) return ReadNumber();
            throw Error("无法识别的字符 " + c.ToString());
        }

        Dictionary<string, object> ReadObject()
        {
            _i++;
            var d = new Dictionary<string, object>();
            SkipWhitespace();
            if (!AtEnd && _s[_i] == '}') { _i++; return d; }
            while (true)
            {
                SkipWhitespace();
                if (AtEnd || _s[_i] != (char)34) throw Error("对象的键必须是字符串");
                string key = ReadString();
                SkipWhitespace();
                Consume(':');
                SkipWhitespace();
                d[key] = ReadValue();
                SkipWhitespace();
                if (!AtEnd && _s[_i] == ',') { _i++; continue; }
                Consume('}');
                return d;
            }
        }

        List<object> ReadArray()
        {
            _i++;
            var list = new List<object>();
            SkipWhitespace();
            if (!AtEnd && _s[_i] == ']') { _i++; return list; }
            while (true)
            {
                SkipWhitespace();
                list.Add(ReadValue());
                SkipWhitespace();
                if (!AtEnd && _s[_i] == ',') { _i++; continue; }
                Consume(']');
                return list;
            }
        }

        string ReadString()
        {
            _i++;
            var sb = new StringBuilder();
            while (true)
            {
                if (AtEnd) throw Error("字符串未结束");
                char c = _s[_i++];
                if (c == (char)34) return sb.ToString();
                if (c != Json.Backslash) { sb.Append(c); continue; }
                if (AtEnd) throw Error("转义序列未结束");
                char e = _s[_i++];
                if (e == (char)34 || e == Json.Backslash || e == '/') sb.Append(e);
                else if (e == 'b') sb.Append((char)8);
                else if (e == 'f') sb.Append((char)12);
                else if (e == 'n') sb.Append((char)10);
                else if (e == 'r') sb.Append((char)13);
                else if (e == 't') sb.Append((char)9);
                else if (e == 'u')
                {
                    if (_i + 4 > _s.Length) throw Error("转义序列不完整");
                    int code;
                    if (!int.TryParse(_s.Substring(_i, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out code))
                        throw Error("转义序列不是十六进制");
                    sb.Append((char)code);
                    _i += 4;
                }
                else throw Error("无效的转义字符 " + e.ToString());
            }
        }

        object ReadNumber()
        {
            int start = _i;
            while (_i < _s.Length)
            {
                char c = _s[_i];
                if ((c >= '0' && c <= '9') || c == '-' || c == '+' || c == '.' || c == 'e' || c == 'E') _i++;
                else break;
            }
            double v;
            if (!double.TryParse(_s.Substring(start, _i - start), NumberStyles.Float, CultureInfo.InvariantCulture, out v))
                throw Error("数字格式错误");
            return v;
        }

        void Expect(string word)
        {
            if (_i + word.Length > _s.Length || string.CompareOrdinal(_s, _i, word, 0, word.Length) != 0)
                throw Error("应为 " + word);
            _i += word.Length;
        }

        void Consume(char c)
        {
            if (AtEnd || _s[_i] != c) throw Error("应为 " + c.ToString());
            _i++;
        }

        FormatException Error(string message)
        {
            return new FormatException("JSON 解析失败：" + message + "（位置 " + _i.ToString(CultureInfo.InvariantCulture) + "）");
        }
    }
}
