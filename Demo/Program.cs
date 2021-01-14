using System;
using System.Linq;

var service = new Demo.Service();

foreach (var city in service.Cities.Download(extraParameters: "outSR=4326", keepQuerying: true, degreeOfParallelism: 4))
{
    var (x, y) = city.Geometry!;
    Console.WriteLine($"{city.OID} {x:F6} {y:F6} {city.Areaname}");
}
