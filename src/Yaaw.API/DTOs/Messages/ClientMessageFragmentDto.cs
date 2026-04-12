namespace Yaaw.API.DTOs.Messages;

public sealed record ClientMessageFragmentDto(
    Guid Id, 
    string Sender, 
    string Text, 
    Guid FragmentId, 
    bool IsFinal = false)
{
    public static ClientMessageFragmentDto CoalesceFragments(List<ClientMessageFragmentDto> fragments)
    {
        var lastFragment = fragments[^1];
        int totalLength = 0;

        for (int i = 0; i < fragments.Count; i++)
        {
            totalLength += fragments[i].Text.Length;
        }

        string combined = string.Create(totalLength, fragments, static (span, frags) =>
        {
            int pos = 0;
            for (int i = 0; i < frags.Count; i++)
            {
                ReadOnlySpan<char> text = frags[i].Text.AsSpan();
                text.CopyTo(span[pos..]);
                pos += text.Length;
            }
        });

        return new ClientMessageFragmentDto(
            lastFragment.Id,
            lastFragment.Sender,
            combined,
            lastFragment.FragmentId,
            lastFragment.IsFinal);
    }
}
