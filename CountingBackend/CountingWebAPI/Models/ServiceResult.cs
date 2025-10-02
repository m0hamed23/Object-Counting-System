namespace CountingWebAPI.Models
{
    public enum ServiceResultStatus
    {
        Success,
        Created,
        NotFound,
        Conflict,
        BadRequest,
        Error
    }

    public class ServiceResult
    {
        public ServiceResultStatus Status { get; protected set; }
        public string? ErrorMessage { get; protected set; }

        public bool IsSuccess => Status == ServiceResultStatus.Success || Status == ServiceResultStatus.Created;

        public static ServiceResult Success(ServiceResultStatus status = ServiceResultStatus.Success) => new ServiceResult { Status = status };
        public static ServiceResult Fail(ServiceResultStatus status, string message) => new ServiceResult { Status = status, ErrorMessage = message };
    }

    public class ServiceResult<T> : ServiceResult
    {
        public T? Value { get; private set; }

        public static ServiceResult<T> Success(T value, ServiceResultStatus status = ServiceResultStatus.Success) => new ServiceResult<T> { Value = value, Status = status };
        public new static ServiceResult<T> Fail(ServiceResultStatus status, string message) => new ServiceResult<T> { Status = status, ErrorMessage = message };
    }
}