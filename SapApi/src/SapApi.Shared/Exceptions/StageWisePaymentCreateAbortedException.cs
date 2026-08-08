namespace SapApi.Shared.Exceptions;

/// <summary>
/// Signals a controlled abort of stage-wise / batch payment create so the ambient
/// DB transaction rolls back (no orphan payment or approval draft rows).
/// </summary>
public sealed class StageWisePaymentCreateAbortedException(string message) : Exception(message);
