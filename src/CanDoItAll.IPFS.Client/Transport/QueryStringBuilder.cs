using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace Ipfs.Engine.Client.Transport
{
    internal static class QueryStringBuilder
    {
        public static void Add(List<KeyValuePair<string, string>> query, string name, string? value)
        {
            if (query == null)
            {
                throw new ArgumentNullException(nameof(query));
            }
            if (string.IsNullOrWhiteSpace(name) || value == null)
            {
                return;
            }

            query.Add(new KeyValuePair<string, string>(name, value));
        }

        public static void Add(List<KeyValuePair<string, string>> query, string name, bool value)
        {
            Add(query, name, value ? "true" : "false");
        }

        public static void Add(List<KeyValuePair<string, string>> query, string name, int value)
        {
            Add(query, name, value.ToString(CultureInfo.InvariantCulture));
        }

        public static void Add(List<KeyValuePair<string, string>> query, string name, long value)
        {
            Add(query, name, value.ToString(CultureInfo.InvariantCulture));
        }

        public static void Add(List<KeyValuePair<string, string>> query, string name, TimeSpan value)
        {
            Add(query, name, Duration.Stringify(value, string.Empty));
        }

        public static void AddRepeated(List<KeyValuePair<string, string>> query, string name, IEnumerable<string?> values)
        {
            if (query == null)
            {
                throw new ArgumentNullException(nameof(query));
            }
            if (values == null)
            {
                return;
            }

            foreach (var value in values.Where(v => v != null))
            {
                Add(query, name, value);
            }
        }

        public static string Build(IEnumerable<KeyValuePair<string, string>>? query)
        {
            if (query == null)
            {
                return string.Empty;
            }

            var builder = new StringBuilder();
            foreach (var pair in query)
            {
                if (builder.Length > 0)
                {
                    builder.Append('&');
                }

                builder.Append(Uri.EscapeDataString(pair.Key));
                builder.Append('=');
                builder.Append(Uri.EscapeDataString(pair.Value));
            }

            return builder.ToString();
        }
    }
}
