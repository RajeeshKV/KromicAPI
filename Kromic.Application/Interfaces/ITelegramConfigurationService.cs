namespace Kromic.Application.Interfaces;

public interface ITelegramConfigurationService
{
    Task ApplyConfigurationAsync(CancellationToken cancellationToken);
}
