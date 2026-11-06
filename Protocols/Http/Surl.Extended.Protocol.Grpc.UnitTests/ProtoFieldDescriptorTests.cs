namespace Surl.Extended.Protocol.Grpc;

/// <summary>
/// A field's JSON name as protoc derives it (descriptor.proto's <c>json_name</c>): underscores dropped,
/// the letter after each upper-cased.
/// </summary>
[TestClass]
public sealed class ProtoFieldDescriptorTests
{
    [TestMethod]
    [DataRow("message", "message")]
    [DataRow("status_code", "statusCode")]
    [DataRow("all_extension_numbers_of_type", "allExtensionNumbersOfType")]
    public void JsonName_FieldName_DropsUnderscoresAndUpperCasesTheLetterAfter(string name, string jsonName)
    {
        var field = new ProtoFieldDescriptor(name, 1, ProtoFieldType.String);

        Assert.AreEqual(jsonName, field.JsonName);
    }
}
