namespace Surl.Cli;

/// <summary>What one <c>;</c>-attribute of a layer-2 item takes (ADR-0101 decision 3).</summary>
internal enum ConfigurationItemAttributeKind
{
    /// <summary>A bare flag, which takes no value (<c>readonly</c>, <c>retain</c>, <c>durable</c>).</summary>
    Flag,

    /// <summary>A key with one non-empty value and no control character (<c>quota</c>, <c>qos</c>).</summary>
    Value,

    /// <summary>A key with a <c>,</c>-separated list of names (<c>protocols</c>, <c>folders</c>).</summary>
    List,
}
