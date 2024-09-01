namespace codecrafters_git.ResultPattern;

public static class ResultExtensions
{
    public static Result<T> Tap<T>(this Result<T> result, Action<T> action)
    {
        if (result.IsSuccess)
        {
            action(result.Response);
        }
        return result;
    }
    public static Result<T> TapError<T>(this Result<T> result, Action<T> action)
    {
        if (!result.IsSuccess)
        {
            action(result.Response);
        }
        return result;
    } 
}