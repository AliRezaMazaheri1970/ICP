namespace Shared.Wrapper;

// پایه
public class Result
{
    public bool Succeeded { get; set; }
    public string[] Messages { get; set; } = Array.Empty<string>();

    internal Result(bool succeeded, IEnumerable<string> messages)
    {
        Succeeded = succeeded;
        Messages = messages as string[] ?? messages.ToArray();
    }

    public static Result Success() => new Result(true, Array.Empty<string>());
    public static Result Fail(string message) => new Result(false, new[] { message });
}

// جنریک
public class Result<T> : Result
{
    public T? Data { get; set; }

    internal Result(bool succeeded, T? data, IEnumerable<string> messages) : base(succeeded, messages)
    {
        Data = data;
    }

    public static Result<T> Success(T data) => new Result<T>(true, data, Array.Empty<string>());
    public static new Result<T> Fail(string message) => new Result<T>(false, default, new[] { message });
}