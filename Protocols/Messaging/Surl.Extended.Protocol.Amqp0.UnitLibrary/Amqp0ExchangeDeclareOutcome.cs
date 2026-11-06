namespace Surl.Extended.Protocol.Amqp0;

/// <summary>What <see cref="Amqp0ExchangeTable.Declare"/> did.</summary>
internal enum Amqp0ExchangeDeclareOutcome
{
    /// <summary>The exchange exists now as declared, made or found.</summary>
    Declared,

    /// <summary>It exists with another type: connection exception 530.</summary>
    TypeDiffers,

    /// <summary>It exists with another <c>durable</c> or other arguments: channel exception 406.</summary>
    FlagsDiffer,

    /// <summary><see cref="Amqp0ExchangeTable.MaxDeclaredExchanges"/> are declared: connection exception 506.</summary>
    TooManyExchanges,
}
