using System;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using Mapsui.Layers;
using SmasKunovice.Avalonia.Extensions;
using SmasKunovice.Avalonia.Models.ConflictResolution;

namespace SmasKunovice.Avalonia.ViewModels;

public partial class ConflictNotification(PointFeature feature, ConflictType conflictType, ConflictLevel conflictLevel) : ObservableObject
{
    private static readonly SolidColorBrush WarningBrush = new(Color.Parse("#FFEB3B")); // Yellow
    private static readonly SolidColorBrush AlarmBrush = new(Color.Parse("#F44336")); // Red
    private static readonly SolidColorBrush GreyBrush = new(Color.Parse("#5B5B5B")); // Grey


    public readonly string UasId = feature.GetScoutDataId();
    public ConflictType ConflictType { get; set; } = conflictType;
    
    [NotifyPropertyChangedFor(nameof(ConflictBrush))] [ObservableProperty]
    private bool _isMuted;

    [NotifyPropertyChangedFor(nameof(ConflictBrush))] [ObservableProperty]
    private ConflictLevel _conflictLevel = conflictLevel;

    [ObservableProperty] private string _aircraftId = feature.GetAircraftDisplayId();

    [ObservableProperty] private string _conflictMessage = conflictType switch
    {
        ConflictType.RunwayApproach => "Runway approach",
        ConflictType.RpaPresence => "RPA presence",
        ConflictType.DroneAboveLimit => "Drone above limit",
        _ => throw new ArgumentOutOfRangeException(nameof(conflictType), conflictType, null)
    };

    public SolidColorBrush ConflictBrush => ConflictLevel switch
    {
        ConflictLevel.Alarm when !IsMuted => AlarmBrush,
        ConflictLevel.Warning when !IsMuted => WarningBrush,
        _ => GreyBrush
    };
}

// public class ConflictLevelToBrushConverter : IValueConverter
// {
//     private static readonly SolidColorBrush WarningBrush = new(Color.Parse("#FFEB3B")); // Yellow
//     private static readonly SolidColorBrush AlarmBrush = new(Color.Parse("#F44336")); // Red
//     private static readonly SolidColorBrush GreyBrush = new(Color.Parse("#2B2B2B")); // Grey
//     public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
//     {
//         if (value is ConflictLevel conflictLevel)
//         {
//             return conflictLevel switch
//             {
//                 ConflictLevel.Alarm => AlarmBrush,
//                 ConflictLevel.Warning => WarningBrush,
//                 _ => GreyBrush
//             };
//         }
//        
//         return GreyBrush;
//     }
//
//     public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
//     {
//         return AvaloniaProperty.UnsetValue;
//     }
// }