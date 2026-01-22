namespace ConnectionManager.Result;

public enum ErrorType
{
    NetworkError,       
    InvalidCredentials, 
    NoBalance,          
    SessionExpired,     
    ParserError,        
    UnexpectedResponse
}

public record Failure(ErrorType Type, string Message, string Details = "");

public class Result<T> : Result
{    
    private readonly T? _value;
    
    public T Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException(
            "No se puede acceder al valor de un resultado fallido.");

    internal Result(T value) : base(true) => _value = value;
    internal Result(Failure error) : base(false, error) => _value = default;
    public static Result<T> Success(T value) => new(value);
    public new static Result<T> Failure(Failure error) => new(error);
}

public class Result
{   
    public bool IsSuccess { get; }
    public bool IsFailure => !IsSuccess;
    public Failure Error { get; }

    protected Result(bool isSuccess, Failure? error = null)
    {        
        if (isSuccess && error != null)
            throw new InvalidOperationException("Un resultado exitoso no puede tener un error.");
        if (!isSuccess && error == null)
            throw new InvalidOperationException("Un resultado fallido debe tener un error.");

        IsSuccess = isSuccess;
        Error = error!;        
    }

    public static Result Success() => new(true);
    public static Result Failure(Failure error) => new(false, error);    
    public static Result<T> Success<T>(T value) => Result<T>.Success(value);
}
