namespace LP2M_Bar_Mngt.Application.Abstractions;

public interface IApplicationDatabaseInitializer
{
    Task InitializeAsync(CancellationToken cancellationToken = default);
}
