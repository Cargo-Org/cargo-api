namespace Cargo.BuildingBlocks.Exceptions;

public sealed class EmailNotVerifiedException(string message) : Exception(message);