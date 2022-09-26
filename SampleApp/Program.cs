using PreStormCore;

foreach (var city in new Usa.Service().Cities.Download(x => x.Areaname!.StartsWith("Ar") && x.Pop2000 > 100000))
{
    var (x, y) = city.Geometry!;
    Console.WriteLine($"{city.OID} {city.Areaname} {city.St} {city.Pop2000} {x} {y}");
}

foreach (var police in new Redlands.Service().Police.Download(x => x.Status == Redlands.UnitStatus.Active))
{
    Console.WriteLine($"{police.OID} {police.Unitname} {police.Status} {police.Lastreport} {police.Geometry?.ToJson()}");
}

foreach (var crime in new Sfcrimes.Service().CrimeLocations.Download(keepQuerying: true, degreeOfParallelism: 4))
{
    Console.WriteLine($"{crime.OID} {crime.Descript} {crime.Datetime} {crime.Geometry?.ToWkt()}");
}
