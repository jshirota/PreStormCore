using System;
using System.Linq;

var service = new Demo.Service();

foreach (var crime in service.CrimeLocations.Download(extraParameters: "outSR=4326", keepQuerying: true, degreeOfParallelism: 4))
{
    var (x, y) = crime.Geometry!;
    Console.WriteLine($"{crime.OID} {x:F6} {y:F6} {crime.Category}");
}
