using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Google.Protobuf.Collections;
using Google.Protobuf.Reflection;
using Google.Protobuf.WellKnownTypes;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Extensions;
using Microsoft.OpenApi.Models;
using Microsoft.OpenApi.Writers;
using Ocelot.Configuration;
using Swashbuckle.AspNetCore.SwaggerGen;
using DevRocks.Ocelot.Grpc.Extensions;
using DevRocks.Ocelot.Grpc.Response;
using DevRocks.Ocelot.Grpc.Swagger.Model;
using Enum = System.Enum;
using Type = System.Type;

namespace DevRocks.Ocelot.Grpc.Swagger.Middleware
{
    internal class SwaggerBuilder
    {
        private readonly SwaggerGeneratorOptions _swaggerGenOptions;
        private readonly GrpcServiceDescriptor _handlers;

        private static readonly Dictionary<Type, Func<OpenApiSchema>> _primitiveTypeMap =
            new Dictionary<Type, Func<OpenApiSchema>>
            {
                [typeof(short)] = () => new OpenApiSchema { Type = "integer", Format = "int32" },
                [typeof(ushort)] = () => new OpenApiSchema { Type = "integer", Format = "int32" },
                [typeof(int)] = () => new OpenApiSchema { Type = "integer", Format = "int32" },
                [typeof(uint)] = () => new OpenApiSchema { Type = "integer", Format = "int32" },
                [typeof(long)] = () => new OpenApiSchema { Type = "integer", Format = "int64" },
                [typeof(ulong)] = () => new OpenApiSchema { Type = "integer", Format = "int64" },
                [typeof(float)] = () => new OpenApiSchema { Type = "number", Format = "float" },
                [typeof(double)] = () => new OpenApiSchema { Type = "number", Format = "double" },
                [typeof(decimal)] = () => new OpenApiSchema { Type = "number", Format = "double" },
                [typeof(byte)] = () => new OpenApiSchema { Type = "integer", Format = "int32" },
                [typeof(sbyte)] = () => new OpenApiSchema { Type = "integer", Format = "int32" },
                [typeof(byte[])] = () => new OpenApiSchema { Type = "string", Format = "byte" },
                [typeof(sbyte[])] = () => new OpenApiSchema { Type = "string", Format = "byte" },
                [typeof(bool)] = () => new OpenApiSchema { Type = "boolean" },
                [typeof(DateTime)] = () => new OpenApiSchema
                    { Type = "string", Format = "date-time", Example = new OpenApiDate(DateTime.Now) },
                [typeof(DateTimeOffset)] = () => new OpenApiSchema
                    { Type = "string", Format = "date-time", Example = new OpenApiDate(DateTime.Now) },
                [typeof(Guid)] = () => new OpenApiSchema { Type = "string", Format = "uuid" },
                [typeof(string)] = () => new OpenApiSchema { Type = "string" },
                [typeof(Timestamp)] = () => new OpenApiSchema
                    { Type = "string", Format = "date-time", Example = new OpenApiDate(DateTime.Now.Date) },
                [typeof(Duration)] = () => new OpenApiSchema
                    { Type = "string", Format = "date-time", Example = new OpenApiString(TimeSpan.Zero.ToString()) },
                [typeof(Google.Protobuf.ByteString)] = () => new OpenApiSchema
                    { Type = "string", Format = "byte", Example = new OpenApiString("U3dhZ2dlciByb2Nrcw==") },
            };

        public SwaggerBuilder(IOptions<SwaggerGeneratorOptions> swaggerGenOptions, GrpcServiceDescriptor handlers,
            Func<IDictionary<Type, Func<OpenApiSchema>>> typeMapFactory)
        {
            _swaggerGenOptions = swaggerGenOptions.Value;
            _handlers = handlers;
            var typesMap = typeMapFactory?.Invoke() ?? new Dictionary<Type, Func<OpenApiSchema>>();
            foreach (var (type, schema) in typesMap)
            {
                _primitiveTypeMap[type] = schema;
            }
        }

        public string BuildSwaggerJson(IReadOnlyCollection<OcelotRouteTemplateTuple> downstreamRoutes)
        {
            var routes = downstreamRoutes.Select(x => x.Route.DownstreamPathTemplate.Value.ToUpper()).ToList();
            var grpcMethods = _handlers.MethodList(routes);

            var doc = new OpenApiDocument
            {
                Info = _swaggerGenOptions.SwaggerDocs.FirstOrDefault().Value,
                Paths = new OpenApiPaths(),
                Components = new OpenApiComponents(),
                Tags = grpcMethods
                    .Select(t => t.Value.Service.Name)
                    .Distinct()
                    .Select(x => new OpenApiTag { Name = x })
                    .ToList()
            };

            var commentStructures = GetXmlDocs(routes);

            foreach (var (key, descriptor) in grpcMethods)
            {
                var routeTuple = downstreamRoutes.Single(x => x.Route.DownstreamPathTemplate.Value.ToUpper() == key);
                var operationType = Enum.Parse<OperationType>(
                    routeTuple.HttpMethods.Single(m => m.Method != OperationType.Options.ToString()).Method, true);
                var httpPath = GetHttpPath(routeTuple.Route);

                commentStructures.TryGetValue((descriptor.Service.Name, descriptor.Name), out var xmlComment);

                var operation = BuildOperation(descriptor, xmlComment, doc.Components.Schemas, httpPath, operationType);

                AppendErrorResponses(operation, doc.Components.Schemas);
                AppendAuthErrorResponses(operation, routeTuple);

                if (doc.Paths.TryGetValue(httpPath, out var path))
                {
                    path.Operations.Add(operationType, operation);
                }
                else
                {
                    doc.Paths.Add(httpPath, new OpenApiPathItem { Operations = { [operationType] = operation } });
                }
            }


            if (_swaggerGenOptions.SecuritySchemes.TryGetValue(SecuritySchemeType.OAuth2.GetDisplayName(),
                out var scheme))
            {
                doc.Components.SecuritySchemes.Add(SecuritySchemeType.OAuth2.GetDisplayName(), scheme);
            }

            doc.Components.Schemas = doc.Components.Schemas.OrderBy(x => x.Key).ToDictionary(x => x.Key, v => v.Value);
            using var writer = new StringWriter();
            doc.SerializeAsV3(new OpenApiJsonWriter(writer));
            return writer.ToString();
        }

        private static OpenApiOperation BuildOperation(MethodDescriptor descriptor, XmlCommentStructure xmlComment,
            IDictionary<string, OpenApiSchema> schemas, string httpPath, OperationType operationType)
        {
            return new OpenApiOperation
            {
                Tags = new[] { new OpenApiTag { Name = descriptor.Service.Name } },
                Summary = xmlComment?.Summary ?? string.Empty,
                Description = xmlComment?.Remarks ?? string.Empty,
                Parameters = BuildParametersList(descriptor, httpPath, operationType),
                RequestBody = BuildRequestBody(schemas, descriptor, operationType),
                Responses = new OpenApiResponses
                {
                    ["200"] = BuildResponse(schemas, descriptor.OutputType, xmlComment)
                },
            };
        }

        private static OpenApiRequestBody BuildRequestBody(IDictionary<string, OpenApiSchema> schemas,
            MethodDescriptor descriptor, OperationType operationType)
        {
            return operationType == OperationType.Get
                ? null
                : new OpenApiRequestBody
                {
                    Content = new Dictionary<string, OpenApiMediaType>
                    {
                        ["application/json"] = new OpenApiMediaType
                        {
                            Schema = new OpenApiSchema
                            {
                                Description = descriptor.FullName,
                                Reference = BuildReference(schemas, descriptor.InputType.ClrType)
                            }
                        }
                    }
                };
        }

        private static IList<OpenApiParameter> BuildParametersList(MethodDescriptor descriptor, string httpPath,
            OperationType operationType)
        {
            var addOnlyPathParams = operationType != OperationType.Get;
            var parametersType = descriptor.InputType.ClrType;
            var result = new List<OpenApiParameter>();
            foreach (var property in parametersType.GetProperties(BindingFlags.Instance | BindingFlags.Public))
            {
                var isPathParam = httpPath.Contains($"{{{ToCamelCase(property.Name)}}}");

                if (addOnlyPathParams && false == isPathParam)
                {
                    continue;
                }

                var parameter = new OpenApiParameter
                {
                    In = isPathParam ? ParameterLocation.Path : ParameterLocation.Query,
                    Name = ToCamelCase(property.Name)
                };
                var propertyType = property.PropertyType;
                if (_primitiveTypeMap.ContainsKey(propertyType))
                {
                    parameter.Schema = _primitiveTypeMap[propertyType]();
                }
                else if (propertyType.IsEnum)
                {
                    parameter.Schema = CreateEnumSchema(propertyType);
                }
                else
                {
                    throw new InvalidOperationException("Unknown parameter type");
                }

                result.Add(parameter);
            }

            return result.Any() ? result : null;
        }

        private static void AppendErrorResponses(OpenApiOperation operation, IDictionary<string, OpenApiSchema> schemas)
        {
            var errorContent = new Dictionary<string, OpenApiMediaType>
            {
                ["object"] = new OpenApiMediaType
                    { Schema = new OpenApiSchema { Reference = BuildReference(schemas, typeof(ApiResponse)) } }
            };
            operation.Responses.Add("408", new OpenApiResponse { Description = "Request timeout" });
            operation.Responses.Add("409", new OpenApiResponse { Description = "Conflict" });
            operation.Responses.Add("400", new OpenApiResponse { Description = "Bad request", Content = errorContent });
            operation.Responses.Add("500",
                new OpenApiResponse { Description = "Internal server error", Content = errorContent });
        }

        private static void AppendAuthErrorResponses(OpenApiOperation operation, OcelotRouteTemplateTuple routeTuple)
        {
            var requiredScopes = routeTuple?.Route?.AuthenticationOptions?.AllowedScopes;
            if (requiredScopes == null || !requiredScopes.Any())
            {
                return;
            }

            operation.Responses.Add("401", new OpenApiResponse { Description = "Unauthorized" });
            operation.Responses.Add("403", new OpenApiResponse { Description = "Forbidden" });

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

        private Dictionary<(string ClassName, string MethodName), XmlCommentStructure> GetXmlDocs(List<string> routes)
        {
            var xmlList = new List<XmlCommentStructure>();
            foreach (var path in _handlers.AssembliesPath(routes))
            {
                var xmlDocumentPath = path.Substring(0, path.Length - 4) + ".xml";
                if (!File.Exists(xmlDocumentPath))
                {
                    continue;
                }

                var lookUp = BuildXmlMemberCommentStructureList(xmlDocumentPath);
                xmlList = xmlList.Concat(lookUp).ToList();
            }

            return xmlList.ToDictionary(x => (x.ClassName, x.MethodName));
        }

        private static OpenApiResponse BuildResponse(IDictionary<string, OpenApiSchema> schemas,
            MessageDescriptor itemOutputType, XmlCommentStructure xmlComment)
        {
            return new OpenApiResponse
            {
                Content = new Dictionary<string, OpenApiMediaType>
                {
                    ["object"] = new OpenApiMediaType
                    {
                        Schema = new OpenApiSchema
                        {
                            Reference = BuildReference(schemas, itemOutputType.ClrType)
                        }
                    }
                },
                Description = xmlComment?.Returns ?? ToCamelCase(itemOutputType.Name)
            };
        }

        private static OpenApiReference BuildReference(IDictionary<string, OpenApiSchema> schemas, Type type)
        {
            var name = type.IsGenericType
                ? ToCamelCase(string.Join("|",
                    new[] { type.Name }.Union(type.GetGenericArguments().Select(x => x.Name))))
                : ToCamelCase(type.Name);

            if (schemas.TryGetValue(name, out var schema))
            {
                return new OpenApiReference { Id = name, Type = ReferenceType.Schema };
            }

            var properties = type.GetProperties(BindingFlags.Instance | BindingFlags.Public);

            var props = properties
                .Select(x =>
                {
                    var propertyType = x.PropertyType;
                    if (_primitiveTypeMap.ContainsKey(propertyType))
                    {
                        return new SchemaName
                        {
                            Name = ToCamelCase(x.Name),
                            Schema = _primitiveTypeMap[propertyType]()
                        };
                    }

                    var swaggerDataType = ToSwaggerDataType(propertyType);

                    return GetSchema(schemas, swaggerDataType, propertyType, x);
                })
                .ToDictionary(x => x.Name, x => x.Schema);

            schema = new OpenApiSchema
            {
                Type = "object",
                Properties = props
            };

            schemas.Add(name, schema);
            return new OpenApiReference
            {
                Id = name,
                Type = ReferenceType.Schema
            };
        }

        private static SchemaName GetSchema(IDictionary<string, OpenApiSchema> definitions, string swaggerDataType,
            Type propertyType, PropertyInfo property)
        {
            if (swaggerDataType == "object")
            {
                if (!TryGetMapFieldTypes(propertyType, out var mapTypes))
                {
                    return new SchemaName
                    {
                        Name = ToCamelCase(property.Name),
                        Schema = new OpenApiSchema
                        {
                            Reference = BuildReference(definitions, propertyType)
                        }
                    };
                }

                return new SchemaName
                {
                    Name = ToCamelCase(property.Name),
                    Schema = new OpenApiSchema
                    {
                        Type = swaggerDataType,
                        Properties = mapTypes
                            .DistinctBy(t => t.Name)
                            .ToDictionary(t => ToCamelCase(t.Name), t => new OpenApiSchema
                            {
                                Type = ToSwaggerDataType(t),
                                Reference = ToSwaggerDataType(t) == "object"
                                    ? BuildReference(definitions, t)
                                    : null
                            })
                    }
                };
            }

            OpenApiSchema items = null;
            if (swaggerDataType == "array")
            {
                var collectionType = GetCollectionType(propertyType);
                var dataType = ToSwaggerDataType(collectionType);
                if (dataType == "object")
                {
                    items = new OpenApiSchema
                    {
                        Reference = BuildReference(definitions, collectionType)
                    };
                }
                else
                {
                    items = collectionType.GetTypeInfo().IsEnum
                        ? CreateEnumSchema(collectionType)
                        : new OpenApiSchema { Type = ToSwaggerDataType(collectionType) };
                }
            }

            return new SchemaName
            {
                Name = ToCamelCase(property.Name),
                Schema = new OpenApiSchema
                {
                    Type = swaggerDataType,
                    Enum = propertyType.GetTypeInfo().IsEnum
                        ? Enum.GetNames(propertyType).Select(e => new OpenApiInteger((int)Enum.Parse(propertyType, e)))
                            .ToList<IOpenApiAny>()
                        : null,
                    Items = items,
                }
            };
        }

        private static OpenApiSchema CreateEnumSchema(Type collectionType)
        {
            return new OpenApiSchema
            {
                Type = "integer",
                Enum = Enum.GetNames(collectionType)
                    .Select(e => new OpenApiInteger((int)Enum.Parse(collectionType, e)))
                    .ToList<IOpenApiAny>(),
            };
        }

        private static Type GetCollectionType(Type type)
        {
            if (type.IsArray)
                return type.GetElementType();

            if (!type.GetTypeInfo().IsGenericType)
                return null; // not collection

            var genTypeDef = type.GetGenericTypeDefinition();
            if (genTypeDef == typeof(IEnumerable<>)
                || genTypeDef == typeof(ICollection<>)
                || genTypeDef == typeof(IList<>)
                || genTypeDef == typeof(List<>)
                || genTypeDef == typeof(IReadOnlyCollection<>)
                || genTypeDef == typeof(IReadOnlyList<>)
                || genTypeDef == typeof(RepeatedField<>)
            )
            {
                return type.GetGenericArguments()[0];
            }

            return null; // not collection
        }

        private static bool TryGetMapFieldTypes(Type type, out IEnumerable<Type> types)
        {
            types = null;
            if (!type.GetTypeInfo().IsGenericType)
                return false; // not MapField

            var genTypeDef = type.GetGenericTypeDefinition();
            if (genTypeDef != typeof(MapField<,>))
                return false; // not MapField

            types = type.GetGenericArguments();
            return true;
        }

        private static IEnumerable<XmlCommentStructure> BuildXmlMemberCommentStructureList(string xmlDocumentPath)
        {
            var file = File.ReadAllText(xmlDocumentPath);
            var xDoc = XDocument.Parse(file);
            var xDocLookup = xDoc
                .Descendants("member")
                .Where(x => x.Attribute("name")?.Value.StartsWith("M:") ?? false)
                .Select(x =>
                {
                    var match = Regex.Match(x.Attribute("name")?.Value ?? throw new ArgumentNullException(),
                        @"(\w+)\.(\w+)?(\(.+\)|$)");

                    var summary = ((string)x.Element("summary")) ?? "";
                    var returns = ((string)x.Element("returns")) ?? "";
                    var remarks = ((string)x.Element("remarks")) ?? "";
                    var parameters = x.Elements("param")
                        .Select(e => (e.Attribute("name")?.Value, e))
                        .Distinct()
                        .ToDictionary(e => e.Item1, e => e.Item2.Value.Trim());

                    return new XmlCommentStructure
                    {
                        ClassName = match.Groups[1].Value,
                        MethodName = match.Groups[2].Value,
                        Summary = summary.Trim(),
                        Remarks = remarks.Trim(),
                        Parameters = parameters,
                        Returns = returns.Trim()
                    };
                });
            return xDocLookup;
        }

        private static string ToSwaggerDataType(Type type)
        {
            if (GetCollectionType(type) != null)
            {
                return "array";
            }

            if (IsNullable(type))
            {
                type = Nullable.GetUnderlyingType(type);
            }


            if (IsDate(type))
            {
                return "string";
            }

            if (type.GetTypeInfo().IsEnum)
            {
                return "integer";
            }

            return Type.GetTypeCode(type) switch
            {
                TypeCode.Boolean => "boolean",
                TypeCode.Decimal => "number",
                TypeCode.Single => "number",
                TypeCode.Double => "number",
                TypeCode.UInt16 => "integer",
                TypeCode.UInt32 => "integer",
                TypeCode.UInt64 => "integer",
                TypeCode.SByte => "integer",
                TypeCode.Byte => "integer",
                TypeCode.Int16 => "integer",
                TypeCode.Int32 => "integer",
                TypeCode.Int64 => "integer",
                TypeCode.Char => "string",
                TypeCode.String => "string",
                _ => "object"
            };
        }

        private static string ToCamelCase(ReadOnlySpan<char> name)
        {
            return char.ToLowerInvariant(name[0]) + name.Slice(1).ToString();
        }

        private static bool IsNullable(Type type)
        {
            return type.GetTypeInfo().IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>);
        }

        private static bool IsDate(Type type)
        {
            return type == typeof(DateTime)
                   || type == typeof(DateTimeOffset);
        }

        private static readonly Regex _endSlashRegex =
            new Regex(@"\/$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static string GetHttpPath(DownstreamRoute route)
            => _endSlashRegex.Replace(route.UpstreamPathTemplate.OriginalValue, string.Empty);

        private class XmlCommentStructure
        {
            public string ClassName { get; set; }
            public string MethodName { get; set; }
            public string Summary { get; set; }
            public string Remarks { get; set; }
            public Dictionary<string, string> Parameters { get; set; }
            public string Returns { get; set; }
        }

        private class SchemaName
        {
            public string Name { get; set; }

            public OpenApiSchema Schema { get; set; }
        }
    }
}