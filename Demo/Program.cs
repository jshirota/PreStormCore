using System;

var service = new MarsTest.Service(user: "aaSysadmin", password: "EsriAdmin1130");

foreach (var property in service.Properties.Download())
{
    Console.WriteLine(property.ShapeLength);
}
