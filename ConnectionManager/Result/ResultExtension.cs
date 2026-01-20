using System.Threading.Tasks;

namespace ConnectionManager.Result;

public static class ResultExtension
{
    public static Result<TNext> Bind<TNext>(
        this Result result, Func<Result<TNext>> next)
    {
        if (result.IsFailure)
            return Result<TNext>.Failure(result.Error);

        return next();
    }

    public static Result<TNext> Bind<TCurrent, TNext>(
        this Result<TCurrent> result, Func<TCurrent, Result<TNext>> next)
    {
        if (result.IsFailure)
            return Result<TNext>.Failure(result.Error);

        return next(result.Value);
    }

    public static async Task<Result<TNext>> BindAsync<TCurrent, TNext>(
        this Task<Result<TCurrent>> task,
        Func<TCurrent, Result<TNext>> next)
    {
        var result = await task; 
        if (result.IsFailure) 
            return Result<TNext>.Failure(result.Error);

        return next(result.Value);
    }

    public static async Task<Result> BindAsync<TCurrent>(
        this Task<Result<TCurrent>> task, Func<TCurrent, Result> next)
    {
        var result = await task;
        if (result.IsFailure) 
            return Result.Failure(result.Error);

        return next(result.Value);
    }

    public static void Fold<TCurrent>(this Result<TCurrent> result,
        Action<TCurrent> OnSuccess, Action<Failure> OnFailure)
    {
        if(result.IsFailure)
            OnFailure(result.Error);
        else
            OnSuccess(result.Value);
    }

    public static async Task<TNext> Fold<TCurrent, TNext>(
        this Task<Result<TCurrent>> task,
            Func<TCurrent, TNext> OnSuccess, 
            Func<Failure, TNext> OnFailure)
    {
        var result = await task;

        if (result.IsFailure)
            return OnFailure(result.Error);
        
        return OnSuccess(result.Value);
    }

    public static async Task Fold<TCurrent>(
        this Task<Result<TCurrent>> task,
            Action<TCurrent> OnSuccess,
            Action<Failure> OnFailure)
    {
        var result = await task;

        if (result.IsFailure)
            OnFailure(result.Error);
        else
            OnSuccess(result.Value);
    }

    public static async Task Fold(this Task<Result> task,
        Action OnSuccess, Action<Failure> OnFailure)
    {
        var result = await task;
        if (result.IsFailure)
            OnFailure(result.Error);
        else
            OnSuccess();
    }
}
