using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using SmasKunovice.Avalonia.Extensions;
using SmasKunovice.Avalonia.Models.ConflictResolution;

namespace SmasKunovice.Avalonia.ViewModels;

public partial class ConflictNotification(ConflictFeature conflictFeature) : ObservableObject
{
    private static readonly SolidColorBrush WarningBrush = new(Color.Parse("#FFEB3B")); // Yellow
    private static readonly SolidColorBrush AlarmBrush = new(Color.Parse("#F44336")); // Red
    private static readonly SolidColorBrush GreyBrush = new(Color.Parse("#5B5B5B")); // Grey
    
     
    [NotifyPropertyChangedFor(nameof(ConflictBrush))]
    [ObservableProperty] private bool _isMuted = conflictFeature.IsMuted;
    
    [NotifyPropertyChangedFor(nameof(ConflictBrush))]
    [ObservableProperty] private ConflictLevel _conflictLevel = conflictFeature.ConflictLevel;
    
    [ObservableProperty] private string _aircraftId = conflictFeature.Feature.GetScoutDataId();
    [ObservableProperty] private string _conflictMessage = conflictFeature.Description;
    
    public SolidColorBrush ConflictBrush => ConflictLevel switch
    {
        ConflictLevel.Alarm when !IsMuted => AlarmBrush,
        ConflictLevel.Warning when !IsMuted=> WarningBrush,
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