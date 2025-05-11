namespace zListBack.Models
{
    public class Result<T>
    {
        public bool Success { get; set; }
        public T? Model { get; set; }
        public string? Message { get; set; }

        public static Result<T> Ok(T? model = default, string? message = null)
        {
            return new Result<T> { Success = true, Model = model, Message = message };
        }

        public static Result<T> Fail(string message)
        {
            return new Result<T> { Success = false, Message = message };
        }
    }

    public class Result : Result<object>
    {
        public static new Result Ok(string? message = null)
        {
            return new Result { Success = true, Message = message };
        }

        public static new Result Fail(string message)
        {
            return new Result { Success = false, Message = message };
        }
    }
}
