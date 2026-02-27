namespace Exterminate.Models;

internal sealed record DeleteResult(bool Success, bool AlreadyGone, string Message);
