using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Microsoft.Extensions.Configuration;

namespace DevRocks.Ocelot.MultiFileConfiguration
{
    public class JsonArrayConfigurationFileParser
    {
        private JsonArrayConfigurationFileParser() { }

        private readonly IDictionary<string, string> _data = new SortedDictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private readonly Stack<string> _context = new Stack<string>();
        private string _currentPath;

        public static IDictionary<string, string> Parse(Stream input, int offset)
            => new JsonArrayConfigurationFileParser().ParseStream(input, offset);

        private IDictionary<string, string> ParseStream(Stream input, int offset)
        {
            _data.Clear();

            var jsonDocumentOptions = new JsonDocumentOptions
            {
                CommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true,
            };

            using var reader = new StreamReader(input);
            using JsonDocument doc = JsonDocument.Parse(reader.ReadToEnd(), jsonDocumentOptions);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
            {
                throw new FormatException( $"Unsupported JSON token '{doc.RootElement.ValueKind}' was found.");
            }
            
            VisitElement(doc.RootElement, offset);

            return _data;
        }

        private void VisitElement(JsonElement element, int offset) 
        {
            foreach (var property in element.EnumerateObject())
            {
                EnterContext(property.Name);
                VisitValue(property.Value, offset);
                ExitContext();
            }
        }

        private void VisitValue(JsonElement value, int offset)
        {
            switch (value.ValueKind) 
            {
                case JsonValueKind.Object:
                    VisitElement(value, offset);
                    break;

                case JsonValueKind.Array:
                    var index = offset;
                    foreach (var arrayElement in value.EnumerateArray()) {
                        EnterContext(index.ToString());
                        VisitValue(arrayElement, offset);
                        ExitContext();
                        index++;
                    }
                    break;

                case JsonValueKind.Number:
                case JsonValueKind.String:
                case JsonValueKind.True:
                case JsonValueKind.False:
                case JsonValueKind.Null:
                    var key = _currentPath;
                    if (_data.ContainsKey(key))
                    {
                        throw new FormatException($"A duplicate key '{key}' was found.");
                    }
                    _data[key] = value.ToString();
                    break;

                case JsonValueKind.Undefined:
                    break;
                default:
                    throw new FormatException($"Unsupported JSON token '{value.ValueKind}' was found.");
            }
        }

        private void EnterContext(string context)
        {
            _context.Push(context);
            _currentPath = ConfigurationPath.Combine(_context.Reverse());
        }

        private void ExitContext()
        {
            _context.Pop();
            _currentPath = ConfigurationPath.Combine(_context.Reverse());
        }
    }
}
