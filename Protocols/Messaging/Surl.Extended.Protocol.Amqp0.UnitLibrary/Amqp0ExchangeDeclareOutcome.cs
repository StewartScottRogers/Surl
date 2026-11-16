namespace Surl.Extended.Protocol.Amqp0;

/// <summary>What <see cref="Amqp0ExchangeTable.Declare"/> did.</summary>
internal enum Amqp0ExchangeDeclareOutcome
{
    /// <summary>The exchange existed and is the same as declared; nothing changed.</summary>
    Declared,

    /// <summary>The exchange did not exist and was made.</summary>
    Created,

    /// <summary>It exists with another type: connection exception 530.</summary>
    TypeDiffers,

    /// <summary>It exists with another <c>durable</c> or other arguments: channel exception 406.</summary>
    FlagsDiffer,

    /// <summary><see cref="Amqp0ExchangeTable.MaxDeclaredExchanges"/> are declared: connection exception 506.</summary>
    TooManyExchanges,
}
