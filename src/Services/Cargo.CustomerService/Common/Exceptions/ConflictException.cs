namespace Cargo.CustomerService.Common.Exceptions;

public sealed class ConflictException(string message) : Exception(message);