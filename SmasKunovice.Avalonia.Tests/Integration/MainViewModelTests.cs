using SmasKunovice.Avalonia.Models.FakeClient;
using SmasKunovice.Avalonia.ViewModels;

namespace SmasKunovice.Avalonia.Tests.Integration;

public class MainViewModelTests
{
    [Test]
    [Explicit("Integration test")]
    public void CreateMap_WhenClientProvided_ShouldCreateMapWithSMASData()
    {
        var vm = new MainViewViewModel(new LogfileDronetagClient(@"C:\\Users\\honza\\codes\\SmasKunovice\\Scripts\\scout_odid_log.json", 2000, new DummyTransformator()));
        vm.CreateMap();
        Task.WaitAll(Task.Delay(5000)); // Wait for the map to be created
    }
}