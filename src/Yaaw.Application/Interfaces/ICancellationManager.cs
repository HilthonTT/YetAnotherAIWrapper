namespace Yaaw.Application.Interfaces;

public interface ICancellationManager
{
    Task CancelAsync(Guid id);
}
