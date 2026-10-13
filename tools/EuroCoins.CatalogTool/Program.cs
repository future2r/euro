using EuroCoins.CatalogTool;

// EuroCoins catalog tool: rebuilds the bundled catalog (src/EuroCoins/Assets/Catalog) from the
// ECB's directory of euro coins.
//
//   dotnet run --project tools/EuroCoins.CatalogTool -- update [--output <catalog folder>]

if (args is not ["update", ..])
{
    Console.WriteLine("""
        Usage:
          EuroCoins.CatalogTool update [--output <catalog folder>]

        Reads all circulation coins and €2 commemoratives from https://www.ecb.europa.eu/euro/coins/,
        downloads new or changed images (normalized to JPEG, max. 540 px) and missing flags, and writes catalog.json.
        IDs of known coins are kept. Default folder: src/EuroCoins/Assets/Catalog.
        """);
    return 1;
}

var output = args is [_, "--output", var folder, ..]
    ? folder
    : Path.Combine(FindRepositoryRoot(), "src", "EuroCoins", "Assets", "Catalog");

using var cancellation = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cancellation.Cancel();
};

using var client = new EcbClient();
return await new CatalogUpdater(client, Path.GetFullPath(output)).RunAsync(cancellation.Token);

static string FindRepositoryRoot()
{
    for (var dir = new DirectoryInfo(Environment.CurrentDirectory); dir is not null; dir = dir.Parent)
    {
        if (File.Exists(Path.Combine(dir.FullName, "EuroCoins.slnx")))
        {
            return dir.FullName;
        }
    }

    throw new DirectoryNotFoundException("Run the tool inside the EuroCoins repository or pass --output.");
}
