using Surl.MessageBus;

namespace Surl.Protocol.Mqtt;

/// <summary>
/// A message published while a subscription was held, waiting to be sent to its subscriber.
/// </summary>
/// <param name="TopicName">The name the subscriber's scheme gives the message's topic.</param>
/// <param name="Message">The message as the bus delivered it.</param>
internal readonly record struct MqttLivePublish(string TopicName, BusMessage Message);
