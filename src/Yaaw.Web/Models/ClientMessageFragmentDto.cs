namespace Yaaw.Web.Models;

public sealed record ClientMessageFragmentDto(
    Guid Id,
    string Sender,
    string Text,
    Guid FragmentId,
    bool IsFinal = false);
