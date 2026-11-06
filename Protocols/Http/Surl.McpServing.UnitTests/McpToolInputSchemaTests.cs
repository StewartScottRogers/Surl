using System.Text.Json;

namespace Surl.McpServing;

[TestClass]
public sealed class McpToolInputSchemaTests
{
    [TestMethod]
    [DataRow("""{"type":"object","properties":{"v":{"type":"string"}}}""", """{"v":"x"}""")]
    [DataRow("""{"type":"object","properties":{"v":{"type":"integer"}}}""", """{"v":3}""")]
    [DataRow("""{"type":"object","properties":{"v":{"type":"integer"}}}""", """{"v":3.0}""")]
    [DataRow("""{"type":"object","properties":{"v":{"type":"number"}}}""", """{"v":3.5}""")]
    [DataRow("""{"type":"object","properties":{"v":{"type":"boolean"}}}""", """{"v":false}""")]
    [DataRow("""{"type":"object","properties":{"v":{"type":"array"}}}""", """{"v":[]}""")]
    [DataRow("""{"type":"object","properties":{"v":{"type":"object"}}}""", """{"v":{}}""")]
    [DataRow("""{"type":"object","properties":{"v":{}}}""", """{"v":null}""")]
    [DataRow("""{"type":"object"}""", """{"anything":1}""")]
    [DataRow("""{"type":"object","properties":{"v":{"enum":["a","b"]}}}""", """{"v":"b"}""")]
    [DataRow("""{"type":"object","properties":{"v":{"minimum":1,"maximum":3}}}""", """{"v":3}""")]
    [DataRow("""{"type":"object","properties":{"v":{"minLength":2,"maxLength":2}}}""", """{"v":"😀😀"}""")]
    [DataRow("""{"type":"object","properties":{"v":{"minItems":1,"maxItems":2,"items":{"type":"string"}}}}""", """{"v":["a","b"]}""")]
    [DataRow("""{"type":"object","properties":{"v":{"items":{"type":"string"}}}}""", """{"v":[]}""")]
    [DataRow("""{"type":"object","properties":{"v":{"type":"array"}}}""", """{"v":[1,"a"]}""")]
    [DataRow("""{"type":"object","additionalProperties":false,"properties":{"v":{}},"required":["v"]}""", """{"v":1}""")]
    [DataRow("""{"type":"object","additionalProperties":true}""", """{"x":1}""")]
    [DataRow("""{"type":"object","title":"t","description":"d","default":{}}""", """{}""")]
    public void Check_ValueMatchingTheSchema_Passes(string schemaText, string value)
    {
        McpToolInputSchema schema = McpToolInputSchema.ReadToolSchema(schemaText, out _);

        Assert.IsNull(schema.Check(Parse(value), "arguments"));
    }

    [TestMethod]
    [DataRow("""{"type":"object","properties":{"v":{"type":"string"}}}""", """{"v":1}""", "arguments.v: expected a string")]
    [DataRow("""{"type":"object","properties":{"v":{"type":"integer"}}}""", """{"v":1.5}""", "arguments.v: expected an integer")]
    [DataRow("""{"type":"object","properties":{"v":{"type":"integer"}}}""", """{"v":1e400}""", "arguments.v: expected an integer")]
    [DataRow("""{"type":"object","properties":{"v":{"type":"integer"}}}""", """{"v":"1"}""", "arguments.v: expected an integer")]
    [DataRow("""{"type":"object","properties":{"v":{"type":"number"}}}""", """{"v":"1"}""", "arguments.v: expected a number")]
    [DataRow("""{"type":"object","properties":{"v":{"type":"boolean"}}}""", """{"v":0}""", "arguments.v: expected a boolean")]
    [DataRow("""{"type":"object","properties":{"v":{"type":"array"}}}""", """{"v":{}}""", "arguments.v: expected an array")]
    [DataRow("""{"type":"object","properties":{"v":{"type":"object"}}}""", """{"v":[]}""", "arguments.v: expected an object")]
    [DataRow("""{"type":"object","properties":{"v":{"enum":["a","b"]}}}""", """{"v":"c"}""", "arguments.v: expected one of a, b")]
    [DataRow("""{"type":"object","properties":{"v":{"enum":["a","b"]}}}""", """{"v":1}""", "arguments.v: expected one of a, b")]
    [DataRow("""{"type":"object","properties":{"v":{"minimum":1}}}""", """{"v":0.5}""", "arguments.v: expected at least 1")]
    [DataRow("""{"type":"object","properties":{"v":{"maximum":2.5}}}""", """{"v":3}""", "arguments.v: expected at most 2.5")]
    [DataRow("""{"type":"object","properties":{"v":{"maximum":2}}}""", """{"v":1e400}""", "arguments.v: expected at most 2")]
    [DataRow("""{"type":"object","properties":{"v":{"minLength":3}}}""", """{"v":"😀😀"}""", "arguments.v: expected at least 3 characters")]
    [DataRow("""{"type":"object","properties":{"v":{"maxLength":1}}}""", """{"v":"ab"}""", "arguments.v: expected at most 1 characters")]
    [DataRow("""{"type":"object","properties":{"v":{"minItems":1}}}""", """{"v":[]}""", "arguments.v: expected at least 1 items")]
    [DataRow("""{"type":"object","properties":{"v":{"maxItems":1}}}""", """{"v":[1,2]}""", "arguments.v: expected at most 1 items")]
    [DataRow("""{"type":"object","properties":{"listen_urls":{"items":{"type":"string"}}}}""", """{"listen_urls":["a",2]}""", "arguments.listen_urls[1]: expected a string")]
    [DataRow("""{"type":"object","required":["a","b"]}""", """{"a":1}""", "arguments.b: required")]
    [DataRow("""{"type":"object","additionalProperties":false,"properties":{"v":{}}}""", """{"v":1,"w":2}""", "arguments.w: not allowed")]
    [DataRow("""{"type":"object","properties":{"o":{"type":"object","properties":{"p":{"type":"boolean"}}}}}""", """{"o":{"p":"x"}}""", "arguments.o.p: expected a boolean")]
    public void Check_ValueBreakingTheSchema_NamesTheFirstFailingPath(string schemaText, string value, string expected)
    {
        McpToolInputSchema schema = McpToolInputSchema.ReadToolSchema(schemaText, out _);

        Assert.AreEqual(expected, schema.Check(Parse(value), "arguments"));
    }

    [TestMethod]
    [DataRow("not json", "The input schema is not JSON.")]
    [DataRow("[]", "inputSchema: must be a schema object.")]
    [DataRow("""{}""", "inputSchema: the type must be \"object\".")]
    [DataRow("""{"type":"string"}""", "inputSchema: the type must be \"object\".")]
    [DataRow("""{"type":"object","oneOf":[]}""", "inputSchema: the keyword oneOf is not one the core checks.")]
    [DataRow("""{"type":7}""", "inputSchema.type: must be a string.")]
    [DataRow("""{"type":"null"}""", "inputSchema.type: the type null is not one the core checks.")]
    [DataRow("""{"type":"object","properties":[]}""", "inputSchema.properties: must be an object.")]
    [DataRow("""{"type":"object","properties":{"v":true}}""", "inputSchema.properties.v: must be a schema object.")]
    [DataRow("""{"type":"object","properties":{"v":{"pattern":"x"}}}""", "inputSchema.properties.v: the keyword pattern is not one the core checks.")]
    [DataRow("""{"type":"object","required":"a"}""", "inputSchema.required: must be an array of strings.")]
    [DataRow("""{"type":"object","required":[1]}""", "inputSchema.required: must be an array of strings.")]
    [DataRow("""{"type":"object","additionalProperties":{}}""", "inputSchema.additionalProperties: must be a boolean.")]
    [DataRow("""{"type":"object","items":3}""", "inputSchema.items: must be a schema object.")]
    [DataRow("""{"type":"object","enum":[1]}""", "inputSchema.enum: must be an array of strings.")]
    [DataRow("""{"type":"object","minimum":"1"}""", "inputSchema.minimum: must be a number.")]
    [DataRow("""{"type":"object","maximum":null}""", "inputSchema.maximum: must be a number.")]
    [DataRow("""{"type":"object","minItems":-1}""", "inputSchema.minItems: must be a non-negative integer.")]
    [DataRow("""{"type":"object","maxItems":1.5}""", "inputSchema.maxItems: must be a non-negative integer.")]
    [DataRow("""{"type":"object","minLength":"1"}""", "inputSchema.minLength: must be a non-negative integer.")]
    [DataRow("""{"type":"object","maxLength":true}""", "inputSchema.maxLength: must be a non-negative integer.")]
    [DataRow("""{"type":"object","description":1}""", "inputSchema.description: must be a string.")]
    [DataRow("""{"type":"object","title":1}""", "inputSchema.title: must be a string.")]
    public void ReadToolSchema_SchemaTheCoreCannotCheck_IsRefused(string schemaText, string message)
    {
        ArgumentException exception = Assert.ThrowsExactly<ArgumentException>(() => McpToolInputSchema.ReadToolSchema(schemaText, out _));

        Assert.AreEqual(message, exception.Message);
    }

    [TestMethod]
    public void ReadToolSchema_SchemaOnSeveralLines_IsKeptAsItsParsedJson()
    {
        McpToolInputSchema.ReadToolSchema("{\n  \"type\": \"object\"\n}", out JsonElement json);

        Assert.AreEqual("object", json.GetProperty("type").GetString());
    }

    private static JsonElement Parse(string json) => JsonDocument.Parse(json).RootElement;
}
