using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace AiPets
{
    /// <summary>JSON object that keeps its key order.</summary>
    sealed class JsonObject
    {
        public readonly List<KeyValuePair<string, object>> Items = new List<KeyValuePair<string, object>>();

        public object Get(string key)
        {
            foreach (KeyValuePair<string, object> kv in Items)
                if (kv.Key == key)
                    return kv.Value;
            return null;
        }

        public void Set(string key, object value)
        {
            for (int i = 0; i < Items.Count; i++)
            {
                if (Items[i].Key == key)
                {
                    Items[i] = new KeyValuePair<string, object>(key, value);
                    return;
                }
            }
            Items.Add(new KeyValuePair<string, object>(key, value));
        }

        public void Remove(string key)
        {
            Items.RemoveAll(kv => kv.Key == key);
        }
    }

    /// <summary>A string, number, true, false or null, kept exactly as it was written.</summary>
    sealed class JsonValue
    {
        public readonly string Raw;

        public JsonValue(string raw)
        {
            Raw = raw;
        }

        /// <summary>The decoded text of a string; null for anything else.</summary>
        public string Text
        {
            get { return Raw.Length >= 2 && Raw[0] == '"' ? Json.Unescape(Raw) : null; }
        }

        public static JsonValue Of(string text)
        {
            return new JsonValue(Json.Quote(text));
        }

        public static JsonValue Of(int number)
        {
            return new JsonValue(number.ToString(CultureInfo.InvariantCulture));
        }

        public static JsonValue Of(bool value)
        {
            return new JsonValue(value ? "true" : "false");
        }
    }

    /// <summary>
    /// Minimal JSON for editing the agents' config files. Objects keep their key order and values their
    /// original text, so writing a file back only changes what was edited (and the indentation).
    /// </summary>
    static class Json
    {
        public static object Parse(string text)
        {
            int i = 0;
            object value = ParseValue(text, ref i);
            SkipSpace(text, ref i);
            if (i < text.Length)
                throw new FormatException("unexpected text after the JSON value at " + i);
            return value;
        }

        /// <summary>Two-space indented JSON with a trailing newline (the style Claude Code and Codex write).</summary>
        public static string Write(object value)
        {
            var sb = new StringBuilder();
            Write(sb, value, 0);
            return sb.Append('\n').ToString();
        }

        public static string Text(object value)
        {
            var v = value as JsonValue;
            return v != null ? v.Text : null;
        }

        public static string Quote(string text)
        {
            var sb = new StringBuilder("\"");
            foreach (char c in text)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < ' ')
                            sb.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                        else
                            sb.Append(c);
                        break;
                }
            }
            return sb.Append('"').ToString();
        }

        public static string Unescape(string raw)
        {
            var sb = new StringBuilder(raw.Length);
            for (int i = 1; i < raw.Length - 1; i++)
            {
                char c = raw[i];
                if (c != '\\' || i + 1 >= raw.Length - 1)
                {
                    sb.Append(c);
                    continue;
                }
                c = raw[++i];
                switch (c)
                {
                    case 'n': sb.Append('\n'); break;
                    case 'r': sb.Append('\r'); break;
                    case 't': sb.Append('\t'); break;
                    case 'b': sb.Append('\b'); break;
                    case 'f': sb.Append('\f'); break;
                    case 'u':
                        if (i + 4 < raw.Length)
                        {
                            sb.Append((char)int.Parse(raw.Substring(i + 1, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture));
                            i += 4;
                        }
                        break;
                    default: sb.Append(c); break;   // \" \\ \/
                }
            }
            return sb.ToString();
        }

        static object ParseValue(string s, ref int i)
        {
            SkipSpace(s, ref i);
            if (i >= s.Length)
                throw new FormatException("unexpected end of JSON");
            char c = s[i];
            if (c == '{')
            {
                var obj = new JsonObject();
                i++;
                SkipSpace(s, ref i);
                if (i < s.Length && s[i] == '}')
                {
                    i++;
                    return obj;
                }
                while (true)
                {
                    SkipSpace(s, ref i);
                    object key = ParseValue(s, ref i);
                    string name = Text(key);
                    if (name == null)
                        throw new FormatException("object key is not a string at " + i);
                    SkipSpace(s, ref i);
                    Expect(s, ref i, ':');
                    obj.Items.Add(new KeyValuePair<string, object>(name, ParseValue(s, ref i)));
                    SkipSpace(s, ref i);
                    if (i < s.Length && s[i] == ',')
                    {
                        i++;
                        continue;
                    }
                    Expect(s, ref i, '}');
                    return obj;
                }
            }
            if (c == '[')
            {
                var list = new List<object>();
                i++;
                SkipSpace(s, ref i);
                if (i < s.Length && s[i] == ']')
                {
                    i++;
                    return list;
                }
                while (true)
                {
                    list.Add(ParseValue(s, ref i));
                    SkipSpace(s, ref i);
                    if (i < s.Length && s[i] == ',')
                    {
                        i++;
                        continue;
                    }
                    Expect(s, ref i, ']');
                    return list;
                }
            }
            int start = i;
            if (c == '"')
            {
                for (i++; i < s.Length && s[i] != '"'; i++)
                    if (s[i] == '\\')
                        i++;
                if (i >= s.Length)
                    throw new FormatException("unterminated string at " + start);
                i++;
                return new JsonValue(s.Substring(start, i - start));
            }
            while (i < s.Length && ",]} \t\r\n".IndexOf(s[i]) < 0)
                i++;
            if (i == start)
                throw new FormatException("unexpected character '" + c + "' at " + i);
            return new JsonValue(s.Substring(start, i - start));
        }

        static void SkipSpace(string s, ref int i)
        {
            while (i < s.Length && (s[i] == ' ' || s[i] == '\t' || s[i] == '\r' || s[i] == '\n' || s[i] == '﻿'))
                i++;
        }

        static void Expect(string s, ref int i, char c)
        {
            if (i >= s.Length || s[i] != c)
                throw new FormatException("expected '" + c + "' at " + i);
            i++;
        }

        static void Write(StringBuilder sb, object value, int indent)
        {
            var obj = value as JsonObject;
            var list = value as List<object>;
            if (obj != null)
            {
                if (obj.Items.Count == 0)
                {
                    sb.Append("{}");
                    return;
                }
                sb.Append("{\n");
                for (int i = 0; i < obj.Items.Count; i++)
                {
                    sb.Append(' ', indent + 2).Append(Quote(obj.Items[i].Key)).Append(": ");
                    Write(sb, obj.Items[i].Value, indent + 2);
                    sb.Append(i + 1 < obj.Items.Count ? ",\n" : "\n");
                }
                sb.Append(' ', indent).Append('}');
            }
            else if (list != null)
            {
                if (list.Count == 0)
                {
                    sb.Append("[]");
                    return;
                }
                sb.Append("[\n");
                for (int i = 0; i < list.Count; i++)
                {
                    sb.Append(' ', indent + 2);
                    Write(sb, list[i], indent + 2);
                    sb.Append(i + 1 < list.Count ? ",\n" : "\n");
                }
                sb.Append(' ', indent).Append(']');
            }
            else
            {
                sb.Append(((JsonValue)value).Raw);
            }
        }
    }
}
