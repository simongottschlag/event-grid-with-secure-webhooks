using DotNext;

namespace Utils;
public static class Invoke
{
    public static Result<TResult> TryInvoke<TResult>(Func<TResult> function)
    {
        Result<TResult> result;
        try
        {
            return function();
        }
        catch (Exception error)
        {
            result = new Result<TResult>(error);
        }

        return result;
    }

    public static Result<TResult> TryInvokeNotNull<TResult>(Func<TResult?> function)
    {
        Result<TResult> result;
        try
        {
            var value = function();
            if (value is null)
            {
                throw new InvalidOperationException("Value is null");
            }
            return value;
        }
        catch (Exception error)
        {
            result = new Result<TResult>(error);
        }

        return result;
    }

    public static async Task<Result<TResult>> TryInvoke<TResult>(Func<Task<TResult>> function)
    {
        Result<TResult> result;
        try
        {
            return await function();
        }
        catch (Exception error)
        {
            result = new Result<TResult>(error);
        }

        return result;
    }

    public static async Task<Result<TResult>> TryInvokeNotNull<TResult>(Func<Task<TResult>> function)
    {
        Result<TResult> result;
        try
        {
            var value = await function();
            if (value is null)
            {
                throw new InvalidOperationException("Value is null");
            }
            return value;
        }
        catch (Exception error)
        {
            result = new Result<TResult>(error);
        }

        return result;
    }
}