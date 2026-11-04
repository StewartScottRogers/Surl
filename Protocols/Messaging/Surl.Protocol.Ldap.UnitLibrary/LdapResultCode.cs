namespace Surl.Protocol.Ldap;

/// <summary>
/// The <c>resultCode</c> of an <c>LDAPResult</c> (RFC 4511, section 4.1.9 and appendix A).
/// </summary>
internal enum LdapResultCode
{
    /// <summary><c>success</c> (0).</summary>
    Success = 0,

    /// <summary><c>operationsError</c> (1).</summary>
    OperationsError = 1,

    /// <summary><c>protocolError</c> (2): the server received malformed or unexpected data.</summary>
    ProtocolError = 2,

    /// <summary><c>timeLimitExceeded</c> (3).</summary>
    TimeLimitExceeded = 3,

    /// <summary><c>sizeLimitExceeded</c> (4).</summary>
    SizeLimitExceeded = 4,

    /// <summary><c>compareFalse</c> (5).</summary>
    CompareFalse = 5,

    /// <summary><c>compareTrue</c> (6).</summary>
    CompareTrue = 6,

    /// <summary><c>authMethodNotSupported</c> (7).</summary>
    AuthMethodNotSupported = 7,

    /// <summary><c>strongerAuthRequired</c> (8).</summary>
    StrongerAuthRequired = 8,

    /// <summary><c>referral</c> (10).</summary>
    Referral = 10,

    /// <summary><c>adminLimitExceeded</c> (11).</summary>
    AdminLimitExceeded = 11,

    /// <summary><c>unavailableCriticalExtension</c> (12).</summary>
    UnavailableCriticalExtension = 12,

    /// <summary><c>confidentialityRequired</c> (13).</summary>
    ConfidentialityRequired = 13,

    /// <summary><c>saslBindInProgress</c> (14).</summary>
    SaslBindInProgress = 14,

    /// <summary><c>noSuchAttribute</c> (16).</summary>
    NoSuchAttribute = 16,

    /// <summary><c>undefinedAttributeType</c> (17).</summary>
    UndefinedAttributeType = 17,

    /// <summary><c>inappropriateMatching</c> (18).</summary>
    InappropriateMatching = 18,

    /// <summary><c>constraintViolation</c> (19).</summary>
    ConstraintViolation = 19,

    /// <summary><c>attributeOrValueExists</c> (20).</summary>
    AttributeOrValueExists = 20,

    /// <summary><c>invalidAttributeSyntax</c> (21).</summary>
    InvalidAttributeSyntax = 21,

    /// <summary><c>noSuchObject</c> (32).</summary>
    NoSuchObject = 32,

    /// <summary><c>aliasProblem</c> (33).</summary>
    AliasProblem = 33,

    /// <summary><c>invalidDNSyntax</c> (34).</summary>
    InvalidDnSyntax = 34,

    /// <summary><c>aliasDereferencingProblem</c> (36).</summary>
    AliasDereferencingProblem = 36,

    /// <summary><c>inappropriateAuthentication</c> (48).</summary>
    InappropriateAuthentication = 48,

    /// <summary><c>invalidCredentials</c> (49).</summary>
    InvalidCredentials = 49,

    /// <summary><c>insufficientAccessRights</c> (50).</summary>
    InsufficientAccessRights = 50,

    /// <summary><c>busy</c> (51).</summary>
    Busy = 51,

    /// <summary><c>unavailable</c> (52).</summary>
    Unavailable = 52,

    /// <summary><c>unwillingToPerform</c> (53).</summary>
    UnwillingToPerform = 53,

    /// <summary><c>loopDetect</c> (54).</summary>
    LoopDetect = 54,

    /// <summary><c>namingViolation</c> (64).</summary>
    NamingViolation = 64,

    /// <summary><c>objectClassViolation</c> (65).</summary>
    ObjectClassViolation = 65,

    /// <summary><c>notAllowedOnNonLeaf</c> (66).</summary>
    NotAllowedOnNonLeaf = 66,

    /// <summary><c>notAllowedOnRDN</c> (67).</summary>
    NotAllowedOnRdn = 67,

    /// <summary><c>entryAlreadyExists</c> (68).</summary>
    EntryAlreadyExists = 68,

    /// <summary><c>objectClassModsProhibited</c> (69).</summary>
    ObjectClassModsProhibited = 69,

    /// <summary><c>affectsMultipleDSAs</c> (71).</summary>
    AffectsMultipleDsas = 71,

    /// <summary><c>other</c> (80).</summary>
    Other = 80,
}
