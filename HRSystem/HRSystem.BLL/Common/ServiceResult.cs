namespace HRSystem.BLL.Common;

// Returned by service operations that can fail on a business rule,
// so controllers can show the message instead of catching exceptions
public class ServiceResult
{
    public bool Succeeded { get; protected init; }
    public string? Error { get; protected init; }

    // Name of the form field the error belongs to (null = whole form)
    public string? Field { get; protected init; }

    public static ServiceResult Success() => new() { Succeeded = true };

    public static ServiceResult Failure(string error, string? field = null)
        => new() { Succeeded = false, Error = error, Field = field };
}

// Same, plus a value on success (e.g. the id of a new record)
public class ServiceResult<T> : ServiceResult
{
    public T? Value { get; private init; }

    public static ServiceResult<T> Success(T value) => new() { Succeeded = true, Value = value };

    public static new ServiceResult<T> Failure(string error, string? field = null)
        => new() { Succeeded = false, Error = error, Field = field };

    public static ServiceResult<T> From(ServiceResult failed)
        => new() { Succeeded = false, Error = failed.Error, Field = failed.Field };
}

public record LookupDto(int Id, string Name);
