using ConnectionManager.Result;

namespace NautaManager;

public static partial class ServiceFailures
{
    public static Result<T> ParseError<T>(
        string message = "Error analizando los datos de conexión",
        string details = "") =>
        Result<T>.Failure(new Failure(
            ErrorType.ParserError, message, details));

    public static Result<T> InvalidCredentials<T>(
        string message = "Error de credenciales",
        string details = "") =>
        Result<T>.Failure(new Failure(
            ErrorType.InvalidCredentials, message, details));

    public static Result<T> SessionExpired<T>(
         string message = "La sesión ha expirado por inactividad.",
         string details = "")
        => Result<T>.Failure(new Failure(
            ErrorType.SessionExpired, message, details));

    public static Result UnexpectedResponse(
        string message = "El servidor esta devolviendo un error.",
        string details = "") =>
        Result.Failure(new Failure(
            ErrorType.UnexpectedResponse, message, details));
}
