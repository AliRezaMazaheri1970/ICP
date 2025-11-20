using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Shared.Wrapper;

// 1. کلاس پایه (غیر جنریک)
public class Result
{
    public bool Succeeded { get; set; }
    public string[] Messages { get; set; }

    // Constructor داخلی
    internal Result(bool succeeded, IEnumerable<string> messages)
    {
        Succeeded = succeeded;
        Messages = messages as string[] ?? Array.Empty<string>();
    }

    // متدهای استاتیک
    public static Result Success()
    {
        return new Result(true, Array.Empty<string>());
    }

    public static Task<Result> SuccessAsync()
    {
        return Task.FromResult(Success());
    }

    public static Result Fail()
    {
        return new Result(false, Array.Empty<string>());
    }

    public static Result Fail(string message)
    {
        return new Result(false, new[] { message });
    }

    public static Task<Result> FailAsync(string message)
    {
        return Task.FromResult(Fail(message));
    }
}

// 2. کلاس جنریک (فرزند)
public class Result<T> : Result
{
    public T? Data { get; set; }

    // Constructor داخلی که Base را صدا می‌زند (حل خطای CS7036)
    internal Result(bool succeeded, T? data, IEnumerable<string> messages) : base(succeeded, messages)
    {
        Data = data;
    }

    // متدهای استاتیک مخصوص نوع T
    public static Result<T> Success(T data, string message = null)
    {
        var messages = message != null ? new[] { message } : Array.Empty<string>();
        return new Result<T>(true, data, messages);
    }

    public static Task<Result<T>> SuccessAsync(T data, string message = null)
    {
        return Task.FromResult(Success(data, message));
    }

    public static new Result<T> Fail(string message)
    {
        return new Result<T>(false, default, new[] { message });
    }

    public static new Task<Result<T>> FailAsync(string message)
    {
        return Task.FromResult(Fail(message));
    }
}