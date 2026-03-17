using Avalonia.Platform;
using Microsoft.Extensions.Options;
using SmasKunovice.Avalonia.Models.Config;
using SmasKunovice.Avalonia.Models.FakeClient;
using SmasKunovice.Avalonia.ViewModels;

namespace SmasKunovice.Avalonia.Tests.Integration;

public class MainViewModelTests
{
    [Test]
    [Explicit("Integration test")]
    public void CreateMap_WhenClientProvided_ShouldCreateMapWithSMASData()
    {
        var vm = new MainViewViewModel(new RandomMessageDronetagClient(), new OptionsWrapper<ApplicationSettings>(new ApplicationSettings(){}));
        vm.CreateMap();
        Task.WaitAll(Task.Delay(5000)); // Wait for the map to be created
    }
}