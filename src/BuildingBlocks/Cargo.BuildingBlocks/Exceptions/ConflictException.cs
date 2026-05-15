namespace Cargo.BuildingBlocks.Exceptions;

public sealed class ConflictException(string message) : Exception(message);