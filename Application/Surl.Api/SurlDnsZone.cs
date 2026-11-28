namespace Surl.Api;

/// <summary>
/// A DNS zone the server holds before any listener binds, held in <see cref="SurlServerOptions.DnsZones"/>. A reset
/// returns to it (ADR-0158, decisions 3 and 4), but a server with a <see cref="SurlServerOptions.DataDirectory"/> or a
/// share on disk refuses the reset (<see cref="SurlServer.ResetAsync"/>). A start refuses a null apex or text with an
/// <see cref="ArgumentException"/>; text that cannot be a zone, a second zone for an apex and a bound passed with a
/// <see cref="SurlSeedRefusedException"/>.
/// </summary>
/// <param name="Apex">The zone's apex, a domain name with or without its final dot.</param>
/// <param name="ZoneFileText">The zone file's text, at most 16 MiB.</param>
public sealed record SurlDnsZone(string Apex, string ZoneFileText);
