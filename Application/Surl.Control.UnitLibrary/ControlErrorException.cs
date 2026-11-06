namespace Surl.Control;

/// <summary>Thrown while a request is read or carried out to answer it with <see cref="Error"/>.</summary>
internal sealed class ControlErrorException : Exception
{
    /// <summary>Creates the exception.</summary>
    /// <param name="error">The answer.</param>
    public ControlErrorException(ControlError error)
        : base(error.Message)
    {
        Error = error;
    }

    /// <summary>The answer.</summary>
    public ControlError Error { get; }
}
