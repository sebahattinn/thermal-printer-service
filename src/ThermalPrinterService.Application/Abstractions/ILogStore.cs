using ThermalPrinterService.Domain.Entities;

namespace ThermalPrinterService.Application.Abstractions;

public interface ILogStore
{
    Task AppendAsync(LogEntry entry, CancellationToken cancellationToken);
    Task<IReadOnlyList<LogEntry>> ReadAllAsync(CancellationToken cancellationToken);
}
