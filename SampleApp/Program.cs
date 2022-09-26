using PreStormCore;

foreach (var city in new Usa.Service().Cities.Download(x => x.Areaname!.StartsWith("Ar") && x.Pop2000 > 100000))
{
    var (x, y) = city.Geometry!;
    Console.WriteLine($"{city.OID} {city.Areaname} {city.St} {city.Pop2000} {x} {y}");
}

await foreach (var county in new Usa.Service().Counties.DownloadAsync(keepQuerying: true))
{
    Console.WriteLine($"{county.OID} {county.Name} {county.StateName} {county.Pop2000} {county.Geometry?.Area()}");
}

foreach (var police in new Redlands.Service().Police.Download(x => x.Status == Redlands.UnitStatus.Active))
{
    Console.WriteLine($"{police.OID} {police.Unitname} {police.Status} {police.Lastreport} {police.Geometry?.ToJson()}");
}

var source = new CancellationTokenSource();

//Task.Run(async () =>
//{
//    await Task.Delay(4000);
//    source.Cancel();
//});

foreach (var crime in new Sfcrimes.Service().CrimeLocations.Download(keepQuerying: true, degreeOfParallelism: 4, cancellationToken: source.Token))
{
    Console.WriteLine($"{crime.OID} {crime.Descript} {crime.Datetime} {crime.Geometry?.ToWkt()}");
}
