
var service = new Usa.Service();

foreach (var state in service.States.Download())
{
    Console.WriteLine(state.StateName);
}
