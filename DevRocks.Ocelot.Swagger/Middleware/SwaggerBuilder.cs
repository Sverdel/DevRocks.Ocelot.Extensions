using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using DevRocks.Ocelot.Swagger.OpenApiSchema;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Extensions;
using Microsoft.OpenApi.Models;
using Microsoft.OpenApi.Writers;
using Newtonsoft.Json;
using Ocelot.Configuration;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace DevRocks.Ocelot.Swagger.Middleware
{
    internal class SwaggerBuilder
    {
        private static readonly Regex _endSlashRegex = new Regex(@"\/$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        
        private readonly SwaggerGeneratorOptions _swaggerGenOptions;

        private readonly JsonSerializerSettings _jsonSerializerSettings = new JsonSerializerSettings
        {
            MetadataPropertyHandling = MetadataPropertyHandling.Ignore,
            NullValueHandling = NullValueHandling.Ignore
        };

        public SwaggerBuilder(IOptions<SwaggerGeneratorOptions> swaggerGenOptions)
        {
            _swaggerGenOptions = swaggerGenOptions.Value;
        }
        
        public string Build(string source, IEnumerable<Route> routes)
        {
            var swagger = JsonConvert.DeserializeObject<SwaggerDocument>(source, _jsonSerializerSettings);
            
            var document = new OpenApiDocument
            {
                Info = swagger.Info ?? _swaggerGenOptions.SwaggerDocs.FirstOrDefault().Value,
                Paths = new OpenApiPaths(),
                Components = new OpenApiComponents
                {
                    SecuritySchemes = _swaggerGenOptions.SecuritySchemes,
                    Schemas = swagger.Components.Schemas?.ToDictionary(x => x.Key, x => BuildSchema(x.Value))
                }
            };
            
            foreach (var route in routes)
            {
                var downstreamroute = route.DownstreamRoute.FirstOrDefault();
                //remove pathSegment specificator
                var downstreamPath = downstreamroute?.DownstreamPathTemplate?.Value?.Replace("*", "");
                if (downstreamPath?.Contains("?") == true)
                    downstreamPath = downstreamPath.Substring(0, downstreamPath.IndexOf('?'));
                var destPath = swagger.Paths.GetValueOrDefault(downstreamPath ?? string.Empty);
                if (downstreamroute == null || destPath == null)
                {
                    continue;
                }

                var (operationType, sourceOperation) = destPath
                    .FirstOrDefault(x => route.UpstreamHttpMethod
                        .Any(m => string.Equals(m.Method, x.Key.ToString(), StringComparison.OrdinalIgnoreCase)));

                var operation = BuildOperation(sourceOperation, downstreamroute);

                //remove pathSegment specificator
                var upstreamPath = downstreamroute.UpstreamPathTemplate.OriginalValue?.Replace("*", "");

                if (string.IsNullOrEmpty(upstreamPath))
                {
                    continue;
                }
                
                if (!document.Paths.ContainsKey(upstreamPath))
                {
                    document.Paths[upstreamPath] = new OpenApiPathItem
                    {
                        Operations = new Dictionary<OperationType, OpenApiOperation>()
                    };
                }

                document
                    .Paths[upstreamPath]
                    .Operations
                    .Add(operationType, operation);
            }

            using var textWriter = new StringWriter(CultureInfo.InvariantCulture);
            document.SerializeAsV3(new OpenApiJsonWriter(textWriter));
            return textWriter.ToString();
        }
        
        private static OpenApiOperation BuildOperation(Operation descriptor, DownstreamRoute route)
        {
            var httpPath = GetHttpPath(route);

            var operation = new OpenApiOperation
            {
                Tags = descriptor.Tags.Select(x => new OpenApiTag { Name = x }).ToList(),
                Summary = descriptor.Summary,
                Description = descriptor.Description,
                Parameters = BuildParametersList(descriptor, httpPath),
                RequestBody = BuildRequestBody(descriptor.RequestBody),
                Responses = BuildResponses(descriptor.Responses)
            };

            AppendAuthErrorResponses(operation, route);

            return operation;
        }

        private static OpenApiResponses BuildResponses(Responses descriptorResponses)
        {
            var responses = new OpenApiResponses();
            if (descriptorResponses == null)
            {
                return responses;
            }

            foreach (var (key, value) in descriptorResponses)
            {
                responses.Add(key, new OpenApiResponse
                {
                    Description = value.Description,
                    Reference = BuildReference(value.Reference),
                    Content = value.Content?.ToDictionary(
                        x => x.Key,
                        x => new OpenApiMediaType
                        {
                            Schema = BuildSchema(x.Value.Schema), 
                        })
                });
            }

            return responses;
        }

        private static IList<OpenApiParameter> BuildParametersList(Operation descriptor, string httpPath)
        {
            var result = new List<OpenApiParameter>();
            if (descriptor.Parameters == null || descriptor.Parameters.Count == 0)
            {
                return result;
            }
            
            foreach (var property in descriptor.Parameters)
            {
                // if UpstreamPathTemplate does not contain current parameter - ignore it
                if (property.In == ParameterLocation.Path
                    && !(httpPath.Contains($"{{{property.Name}}}", StringComparison.OrdinalIgnoreCase) 
                        || httpPath.Contains($"{{*{property.Name}}}", StringComparison.OrdinalIgnoreCase))) // pathSegment with specificator * (for example {*aliasPath})
                {
                    continue;
                }

                var parameter = new OpenApiParameter
                {
                    In = property.In,
                    Name = property.Name,
                    Schema = BuildSchema(property.Schema),
                    Required = property.Required,
                    Description = property.Description,
                };

                result.Add(parameter);
            }

            return result.Any() ? result : null;
        }

        private static Microsoft.OpenApi.Models.OpenApiSchema BuildSchema(Schema schema)
        {
            if (schema == null)
            {
                return null;
            }

            return new Microsoft.OpenApi.Models.OpenApiSchema
            {
                Type = schema.Type,
                Format = schema.Format,
                Enum = schema.Enum?.Select(x => GetEnumMember(schema.Type, x)).ToList(),
                Description = schema.Description,
                Items = BuildSchema(schema.Items),
                Maximum = schema.Maximum,
                Minimum = schema.Minimum,
                Nullable = schema.Nullable,
                Pattern = schema.Pattern,
                Properties = schema.Properties?.ToDictionary(x => x.Key, x => BuildSchema(x.Value)),
                Reference = BuildReference(schema.Reference),
                Title = schema.Title,
                ReadOnly = schema.ReadOnly,
                WriteOnly = schema.WriteOnly,
                Required = schema.Required,
                Default = string.IsNullOrEmpty(schema.Default) ? null : new OpenApiString(schema.Default),
            };
        }

        private static IOpenApiAny GetEnumMember(string type, object x)
        {
            return type switch
            {
                "integer" => new OpenApiLong((long)x),
                _ => new OpenApiString((string)x)
            };
        }

        private static OpenApiReference BuildReference(string reference)
        {
            return string.IsNullOrEmpty(reference) 
                ? null 
                : new OpenApiReference
                {
                    ExternalResource = reference,
                };
        }

        private static OpenApiRequestBody BuildRequestBody(RequestBody body)
        {
            if (body == null)
            {
                return null;
            }

            return new OpenApiRequestBody
            {
                Content = body.Content?.ToDictionary(
                    x => x.Key,
                    x => new OpenApiMediaType
                    {
                        Schema = BuildSchema(x.Value?.Schema),
                    })
            };
        }

        private static void AppendAuthErrorResponses(OpenApiOperation operation, DownstreamRoute route)
        {
            var requiredScopes = route?.AuthenticationOptions?.AllowedScopes;
            if (requiredScopes == null || !requiredScopes.Any())
            {
                return;
            }

            operation.Responses.TryAdd("401", new OpenApiResponse { Description = "Unauthorized" });
            operation.Responses.TryAdd("403", new OpenApiResponse { Description = "Forbidden" });

            var schema = new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Id = SecuritySchemeType.OAuth2.GetDisplayName(), Type = ReferenceType.SecurityScheme
                }
            };

            operation.Security = new List<OpenApiSecurityRequirement>
            {
                new OpenApiSecurityRequirement { [schema] = requiredScopes }
            };
        }

        private static string GetHttpPath(DownstreamRoute route)
            => _endSlashRegex.Replace(route.UpstreamPathTemplate.OriginalValue, string.Empty);
    }
}
