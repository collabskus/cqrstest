namespace MultiDbSync.Console;

public sealed class DemoSettings
{
    public required int ProductCount { get; init; }
    public required int StockUpdateCount { get; init; }
    public required int PriceUpdateCount { get; init; }
    public required int DeleteCount { get; init; }
    public required int RandomSeed { get; init; }
}
