using System;

var service = new Sfcrimes.Service();

foreach (var state in service.CrimeLocations.Download())
{
    Console.WriteLine(state);
}
