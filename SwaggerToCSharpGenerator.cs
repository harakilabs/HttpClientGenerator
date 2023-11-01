using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Newtonsoft.Json.Linq;

namespace HttpClientGenerator
{
    public class SwaggerToCSharpGenerator
    {
        private readonly string _swaggerJson;
        private readonly JObject _swaggerObject;
        private string _basePath;

        public SwaggerToCSharpGenerator(string swaggerJsonPath)
        {
            _swaggerJson = File.ReadAllText(swaggerJsonPath);
            _swaggerObject = JObject.Parse(_swaggerJson);
        }

        public void Generate()
        {
            GenerateClient();
            GeneratePayloads();
        }

        private void GenerateClient()
        {
            string title = _swaggerObject["info"]["title"].ToString().Replace(" ", "");
            string httpClientClassName = $"{title}HttpClient";
            _basePath = $"{httpClientClassName}";
            Directory.CreateDirectory(_basePath);
            string classContent = GenerateHttpClientClass(httpClientClassName);
            File.WriteAllText($"{_basePath}/{httpClientClassName}.cs", classContent);
        }

        private string GenerateHttpClientClass(string className)
        {
            className = _swaggerObject["info"]["title"].ToString().Replace(".", "") + "V" + _swaggerObject["info"]["version"].ToString().Replace("v", "").Replace("V", "") + "HttpClient";
            string methodTemplate = @"
public async Task<Result<{0}>> {1}Async({2})
{{
    var request = new HttpRequestMessage(HttpMethod.{3}, $""{4}"");
    {5}
    return await SendRequestAsync<{0}>(request);
}}";

            string methodsContent = "";

            foreach (var pathPair in _swaggerObject["paths"].ToObject<JObject>())
            {
                string pathKey = pathPair.Key;
                JObject pathValue = (JObject)pathPair.Value;

                foreach (var operationPair in pathValue)
                {
                    string httpMethod = operationPair.Key.ToString().ToUpper();
                    JObject operationValue = (JObject)operationPair.Value;

                    string operationId = ToPascalCase($"{pathPair.Key.Replace("/", "")}{operationPair.Key.ToString().ToUpper()}");

                    string returnType = "object";

                    string parametersContent = "";
                    if (operationValue["parameters"] != null)
                    {
                        JArray parameters = (JArray)operationValue["parameters"];
                        foreach (var parameter in parameters)
                        {
                            string parameterName = parameter["name"].ToString();
                            string parameterType = parameter["schema"]?["$ref"]?.ToString().Split('/').Last() ?? "object";
                            parametersContent += $"{ConvertToCSharpType(parameterType)} {parameterName}, ";
                        }
                    }

                    string contentContent = "";
                    if (operationValue["requestBody"] != null)
                    {
                        contentContent = "var content = new StringContent(JsonSerializer.Serialize(requestBody), System.Text.Encoding.UTF8, \"application/json\");\n            request.Content = content;";
                        string requestBodyType = operationValue["requestBody"]["content"]["application/json"]["schema"]["$ref"]?.ToString().Split('/').Last() ?? "object";
                        parametersContent += $"{requestBodyType} requestBody, ";
                    }

                    methodsContent += string.Format(methodTemplate, returnType, operationId, parametersContent.TrimEnd(',', ' '), httpMethod, pathKey, contentContent);
                }
            }

            return $@"
using HttpClientGenerator.HttpClientBase;
using System.Net.Http;
using FluentResults;
using System.Text.Json;
using System.Threading.Tasks;

public class {className} : BaseApiClient
{{
    {methodsContent}
}}";
        }



        private void GeneratePayloads()
        {
            string outputDirectory = $"{_swaggerObject["info"]["title"].ToString().Replace(" ", "")}HttpClient";
            Directory.CreateDirectory(outputDirectory);

            JObject schemas = (JObject)_swaggerObject["components"]["schemas"];
            foreach (var schema in schemas)
            {
                string name = schema.Key;
                JToken schemaValue = schema.Value;

                if (schemaValue["enum"] != null)
                {
                    string enumContent = GenerateEnum(name, schemaValue["enum"].ToObject<JArray>());
                    File.WriteAllText($"{outputDirectory}\\{name}.cs", enumContent);
                }
                else if (schemaValue["properties"] != null)
                {
                    JObject properties = (JObject)schemaValue["properties"];
                    string classContent = GenerateClass(name, properties);
                    File.WriteAllText($"{outputDirectory}\\{name}.cs", classContent);
                }
            }
        }

        private string GenerateEnum(string enumName, JArray enumValues)
        {
            StringBuilder enumBuilder = new StringBuilder();
            enumBuilder.AppendLine($"public enum {enumName}");
            enumBuilder.AppendLine("{");
            foreach (var enumValue in enumValues)
            {
                enumBuilder.AppendLine($"    {enumValue},");
            }
            enumBuilder.AppendLine("}");
            return enumBuilder.ToString();
        }


        private string GenerateClass(string className, JObject properties)
        {
            if (properties is null) return string.Empty;

            string classTemplate = "public class {0}\n{{\n{1}\n}}";
            string propertyTemplate = "    public {0} {1} {{ get; set; }}";

            List<string> propertyLines = new List<string>();
            foreach (var property in properties)
            {
                string propertyName = ConvertToPascalCase(property.Key);
                string propertyType = ConvertToCSharpType(property.Value["type"]?.ToString());
                propertyLines.Add(string.Format(propertyTemplate, propertyType, propertyName));
            }

            return string.Format(classTemplate, className, string.Join("\n", propertyLines));
        }

        private string ConvertToCSharpType(string jsonType)
        {
            if (jsonType.Contains("."))
            {
                var parts = jsonType.Split('.');
                return ConvertToPascalCase(parts[parts.Length - 2]) + "." + ConvertToPascalCase(parts[parts.Length - 1]);
            }

            return jsonType switch
            {
                "integer" => "int",
                "string" => "string",
                "boolean" => "bool",
                "array" => "List<object>", // Adjust for specific list types if needed
                _ => "object",
            };
        }

        private string ConvertToPascalCase(string input)
        {
            return string.IsNullOrEmpty(input) ? input : char.ToUpperInvariant(input[0]) + input.Substring(1);
        }

        private string ToPascalCase(string input)
        {
            StringBuilder result = new StringBuilder();
            bool nextUpper = true;

            foreach (char c in input)
            {
                if (c == '-' || c == '/')
                {
                    nextUpper = true;
                    continue;
                }

                if (nextUpper)
                {
                    result.Append(char.ToUpper(c));
                    nextUpper = false;
                }
                else
                {
                    result.Append(char.ToLower(c));
                }
            }

            return result.ToString();
        }

    }
}
