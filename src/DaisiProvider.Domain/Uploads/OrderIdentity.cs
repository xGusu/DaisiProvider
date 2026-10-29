namespace DaisiProvider.Domain.Uploads;

public sealed record OrderIdentity(string OrderId, DateTimeOffset Timestamp);
