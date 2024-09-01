namespace codecrafters_git.ResultPattern;

public class Result<T>
{
    private readonly object _lock = new object();
    private T _response;
    private readonly List<Error> _errors = new List<Error>();
    
    public bool IsSuccess
    {
        get
        {
            lock (_lock)
            {
                return  _errors.Count == 0;  
            }
        }
    }

    public bool IsFailure => !IsSuccess;
    public T Response
    {
        get
        {
            lock (_lock)
            {
                return _response;
            }
        }
        private set
        {
            lock (_lock)
            {
                _response = value;
            }
        }
    }
    public List<Error> Errors
    {
        get
        {
            lock (_lock)
            {
                return new List<Error>(_errors);
            }
        }
    }

    public Error Error
    {
        get
        {
            lock (_lock)
            {
                return _errors.LastOrDefault();
            }
        }
    }

    public static Result<T> Create(T response)
    {
        return new Result<T>(response);
    }

    public static Result<T> Create(Error error)
    {
        return new Result<T>(error);
    }

    public static Result<T> Create(List<Error> errors)
    {
        return new Result<T>(errors);
    }
    private Result(T response)
    {
        Response = response;
    }

    private Result(Error error)
    {
        lock (_lock)
        {
            _errors.Add(error);
        }
    }

    private Result(List<Error> errors)
    {
        lock (_lock)
        {
            _errors.AddRange(errors);
        }
    }

    public void AddError(Error error)
    {
        lock (_lock)
        {
            _errors.Add(error);
        }
    }
    
    public Result<U> Bind<U>(Func<T, Result<U>> func)
    {
        if (IsFailure)
            return Result<U>.Create(Error);

        return func(Response);
    }
    public static Result<T> TryExecute<T>(Func<T> action, Func<Exception, Error> errorHandler)
    {
        try
        {
            return Result<T>.Create(action());
        }
        catch (Exception ex)
        {
            return Result<T>.Create(errorHandler(ex));
        }
    }
}