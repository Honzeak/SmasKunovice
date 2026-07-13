using System.Collections.ObjectModel;
using System.Linq;
using Mapsui.Layers;
using SmasKunovice.Avalonia.Extensions;
using SmasKunovice.Avalonia.Models.ConflictResolution;

namespace SmasKunovice.Avalonia.ViewModels;

public class ConflictNotificationCollection : ObservableCollection<ConflictNotification>
{
    public void UpdateConflictNotification(PointFeature feature, ConflictType conflictType, ConflictLevel conflictLevel)
    {
        var notification = this.FirstOrDefault(notification => notification.UasId == feature.GetScoutDataId() && notification.ConflictType == conflictType);

        notification?.ConflictLevel = conflictLevel;
    }
}