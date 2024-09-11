namespace codecrafters_git.ResultPattern;

public static class ResultExtensions
{
    public static Result<U> Bind<T, U>(this Result<T> result, Func<T, Result<U>> func)
    {
        if (result.IsFailure)
        {
            return Result<U>.Failure(result.Error);
        }

        return func(result.Response);
    }
    public static Result<U> Bind<T, U>(this Task<Result<T>> task, Func<T, Result<U>> func)
    {
        var result = task.Result;

        if (result.IsFailure)
        {
            return Result<U>.Failure(result.Error);
        }

        return func(result.Response);
    }
 
    public static async Task<Result<U>> BindAsync<T, U>(this Task<Result<T>> task, Func<T, Task<Result<U>>> func)
    {
        var result = await task;
        if (result.IsFailure)
        {
            return Result<U>.Failure(result.Error);
        }

        return await func(result.Response);
    }
    public static async Task<Result<U>> BindAsync<T, U>(this Result<T> result, Func<T, Task<Result<U>>> func)
    {
        if (result.IsFailure)
        {
            return Result<U>.Failure(result.Error);
        }

        return await func(result.Response);
    }

    public static Result<T> Tap<T>(this Result<T> result, Action<T> action)
    {
        if (result.IsSuccess)
        {
            action(result.Response);
        }

        return result;
    }
    public static async Task<Result<T>> TapAsync<T>(this Task<Result<T>> task, Action<Result<T>> action)
    {
        var result = await task;
        if (result.IsSuccess)
        {
            action(result);
        }

        return result;
    }
    public static Result<T> TapError<T>(this Result<T> result, Action<IEnumerable<Error>> action)
    {
        if (result.IsFailure)
        {
            action(result.Errors);
        }
        return result;
    }
    public static async Task<Result<T>> TapErrorAsync<T>(this Task<Result<T>> task, Func<IEnumerable<Error>, Task> action)
    {
        var result = await task;
        if (result.IsFailure && result.Errors.Any())
        {
            await action(result.Errors); // Process all errors
        }
        return result;
    }
    public static async Task<Result<T>> TapErrorAsync<T>(this Task<Result<T>> task, Action<Result<T>> action)
    {
        var result = await task;
        if (result.IsFailure)
        {
            action(result);  // Execute synchronous action
        }
        return result;
    }public static Result<T> TapFirstError<T>(this Result<T> result, Func<Error, Task> action)
    {
    
        if (result.IsFailure && result.Errors.Any())
        {
            action(result.Errors.First());
        }
        return result;
    }
    public static async Task<Result<T>> TapFirstErrorAsync<T>(this Task<Result<T>> task, Func<Error, Task> action)
    {
        var result = await task;
        if (result.IsFailure && result.Errors.Any())
        {
            await action(result.Errors.First());
        }
        return result;
    }
    public static async Task<Result<T>> TryExecuteAsync<T>(Func<Task<T>> action, Func<Exception, IEnumerable<Error>> errorHandler)
    {
        try
        {
            var result = await action();
            return Result<T>.Success(result);
        }
        catch (Exception ex)
        {
            return Result<T>.Failure(errorHandler(ex).ToList());
        }
    }
    public static Result<T> TryExecute<T>(Func<T> action, Func<Exception, IEnumerable<Error>> errorHandler)
    {
        try
        {
            return Result<T>.Success(action());
        }
        catch (Exception ex)
        {
            return Result<T>.Failure(errorHandler(ex).ToList());
        }
    }
}