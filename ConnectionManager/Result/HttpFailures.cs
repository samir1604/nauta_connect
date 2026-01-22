namespace ConnectionManager.Result;

public static class HttpFailures
{
    public static Result<T> UnexpectedResponse<T>(
        string message = "El servidor esta devolviendo un error.", 
        string details = "") =>
        Result<T>.Failure(new Failure(
            ErrorType.UnexpectedResponse, message, details));

    public static Result<T> NetworkError<T>(
        string message = "Error de conexión.", string details = "") =>
            Result<T>.Failure(new Failure(
                ErrorType.NetworkError, message, details));

    

    public static Result<T> InvalidCredentials<T>()
        => Result<T>.Failure(new Failure(
            ErrorType.InvalidCredentials, 
            "Usuario o contraseña incorrectos."));
    
    public static Result Ensure(bool condition, Failure error)
        => condition ? Result.Success() : Result.Failure(error);

}
