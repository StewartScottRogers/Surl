using Surl.Authentication;
using Surl.Composition;
using Surl.Content;
using Surl.MailStore;

namespace Surl.Api;

/// <summary>
/// What a test's own protocol server (<see cref="SurlProtocolServer"/>) is built from: the instance's stores, its
/// accounts and its clock (ADR-0124, decision 2), the same objects the built-in servers share, so a cross-protocol
/// test sees one store.
/// </summary>
public sealed class SurlProtocolServices
{
    internal SurlProtocolServices(ComposedProtocolServices services)
    {
        ContentStore = services.ContentStore;
        MailStore = services.MailStore;
        MessageBus = services.MessageBus;
        AuthenticationPolicy = services.AuthenticationPolicy;
        TimeProvider = services.TimeProvider;
    }

    /// <summary>Gets the content store the file-serving servers serve.</summary>
    public ContentStore ContentStore { get; }

    /// <summary>Gets the mail store the SMTP server delivers into and the IMAP and POP3 servers serve.</summary>
    public MailboxStore MailStore { get; }

    /// <summary>Gets the message bus the messaging servers share.</summary>
    public MessageBus.MessageBus MessageBus { get; }

    /// <summary>Gets the policy logins are judged by: every account, as <see cref="SurlServerOptions.Accounts"/> and the rest give them.</summary>
    public AuthenticationPolicy AuthenticationPolicy { get; }

    /// <summary>Gets the one clock, <see cref="SurlServerOptions.TimeProvider"/>.</summary>
    public TimeProvider TimeProvider { get; }
}
