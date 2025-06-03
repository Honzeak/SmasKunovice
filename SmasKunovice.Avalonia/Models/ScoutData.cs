using System.Text.Json;
using Mapsui.Layers;

namespace SmasKunovice.Avalonia.Models;

public interface IScoutData
{
    public bool TryCreatePointFeature(out PointFeature? pointFeature);
}
public record ScoutData : IScoutData
{
    public bool TryCreatePointFeature(out PointFeature? pointFeature)
    {
        pointFeature = null;
        if (Odid.Location?.Longitude is null || Odid.Location.Latitude is null)
            return false;
        
        pointFeature = new PointFeature((double)Odid.Location.Latitude, (double)Odid.Location.Longitude);
        pointFeature["ID"] = Odid.BasicId[0].UasId;
        pointFeature["ScoutData"] = this;
        return true;
    }

    public static JsonSerializerOptions SerializerOptions { get; } = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        Converters = { new StringOrStringArrayConverter() }
    };

    /// <summary>
    /// Reported RSSI by the receiving module
    /// </summary>
    public int Rssi { get; init; }

    /// <summary>
    /// Receiving technology: B4 (Bluetooth legacy), B5 (Bluetooth LE), WN (Wi-Fi Nan), WB (Wi-Fi Beacon)
    /// </summary>
    public string[]? Tech { get; init; }

    /// <summary>
    /// Module number that received the message. Corresponds to the antenna position on the box.
    /// </summary>
    public int RecvId { get; init; }

    /// <summary>
    /// Module type that received the message. Mainly for internal use.
    /// </summary>
    public int ModuleId { get; init; }

    /// <summary>
    /// ODID message type as defined in the reference: BASIC_ID (0), LOCATION (1), AUTH (2), SELF_ID (3),
    /// SYSTEM (4), OPERATOR_ID (5), PACKED (15), INVALID (255); we don't forward AUTH messages
    /// </summary>
    public int MsgType { get; init; }
    public required OdidData Odid { get; init; }
}

public record OdidData
{
    public required BasicIdData[] BasicId { get; init; }

    /// <summary>
    /// see table 1.3 Position of the aircraft. This will be the most common message over B4. Other techs use
    /// mainly PACKED messages with location included. Used units: M - meters, M/S - meters per
    /// second, NM - nautical miles (1.852 km).
    /// </summary>
    public LocationData? Location { get; init; }

    /// <summary>
    /// see table 1.4 The Self-ID Message is optionally sent in case the Remote Pilot wishes to declare its
    /// identity, flight purpose or both. This may serve as mitigation of a perceived threat by a
    /// neighbouring person or public in case a UA is operating in the same close area.
    /// </summary>
    public SelfIdData? SelfId { get; init; }

    /// <summary>
    /// see table 1.5 Contains information about the Remote Pilot location and a swarm (if applicable).
    /// </summary>
    public SystemData? System { get; init; }

    /// <summary>
    /// see table 1.6 UAS Operator Registration Number.
    /// </summary>
    public OperatorIdData? OperatorId { get; init; }
}
// Class to hold the JSON/ODID data structure, mirroring the document

public record BasicIdData
{
    /// <summary>
    /// UNKNOWN = 0. AEROPLANE = 1, HELICOPTER_OR_MULTIROTOR = 2, GYROPLANE = 3, HYBRID_LIFT = 4,
    /// ORNITHOPTER = 5, GLIDER = 6, KITE = 7 FREE_BALLOON = 8, CAPTIVE_BALLOON = 9, AIRSHIP = 10,
    /// FREE_FALL_PARACHUTE = 11, ROCKET = 12, TETHERED_POWERED_AIRCRAFT = 13, GROUND_OBSTACLE = 14, OTHER = 15
    /// </summary>
    public int UaType { get; init; }

    /// <summary>
    /// NONE = 0, SERIAL_NUMBER = 1, CAA_REGISTRATION_ID = 2, UTM_ASSIGNED_UUID = 3, SPECIFIC_SESSION_ID = 4
    /// </summary>
    public int IdType { get; init; }

    /// <summary>
    /// e.g.: 1596F350457791312042
    /// </summary>
    public required string UasId { get; init; }
}

public record LocationData
{
    /// <summary>
    /// UNDECLARED = 0, GROUND = 1, AIRBORNE = 2, EMERGENCY = 3, REMOTE_ID_SYSTEM_FAILURE = 4
    /// </summary>
    public int Status { get; init; }

    /// <summary>
    /// 0-360 degrees
    /// </summary>
    public int? Direction { get; init; }

    /// <summary>
    /// 0.0-255.0 m/s.
    /// Positive only. Invalid, if speed is >= 254.25 m/s: 254.25m/s
    /// </summary>
    public float? SpeedHorizontal { get; init; }

    /// <summary>
    /// m/s. Invalid, No Value, or Unknown: 63m/s. If speed is >= 62m/s: 62m/s
    /// </summary>
    public float? SpeedVertical { get; init; }

    /// <summary>
    /// -180-+180; 7 decimal places
    /// </summary>
    public float? Longitude { get; init; }

    /// <summary>
    /// -90- +90; 7 decimal places
    /// </summary>
    public float? Latitude { get; init; }

    /// <summary>
    /// meter (Ref 29.92 inHg, 1013.24 mb)
    /// </summary>
    public float? AltitudeBaro { get; init; }

    /// <summary>
    /// meter (WGS84-HAE)
    /// </summary>
    public float? AltitudeGeo { get; init; }

    /// <summary>
    /// OVER_TAKEOFF = 0, OVER_GROUND = 1
    /// </summary>
    public int HeightType { get; init; }

    /// <summary>
    /// meters; can be negative
    /// </summary>
    public float? Height { get; init; }

    /// <summary>
    /// UNKNOWN=0, 10NM=1, 4NM=2, 2NM=3, 1NM=4, 5NM=5.
    /// 0_3NM=6, 0_ 1 NM=7 05NM=8, 30M=9, 10M=10, 3M=11, 1M=12
    /// </summary>
    public int HorizAccuracy { get; init; }

    /// <summary>
    /// UNKNOWN = 0, 150M = 1, 45M = 2, 25M = 3, 10M = 4, 3M = 5, 1M = 6
    /// </summary>
    public int VertAccuracy { get; init; }

    /// <summary>
    /// the same as VertAccuracy
    /// </summary>
    public int BaroAccuracy { get; init; }

    /// <summary>
    /// UNKNOWN=0, 10M/S=1, 3M/S=2, 1 M/S=3, 0.3M/S=4
    /// </summary>
    public int SpeedAccuracy { get; init; }

    /// <summary>
    /// UNKNOWN=0, 0.1s=1, 0.2s=2, 0.3s=3, 0.4s=4, 0.5s=5, 0.6s=6,
    /// 0.7s=7, 0.8s=8, 0.9s=9, 1.0s=10, 1.1s=11, 1.2s=12, 1.3s=13, 1.4s=14,
    /// 1.5s=15
    /// </summary>
    public int TsAccuracy { get; init; }

    /// <summary>
    /// date-time in ISO 8601 format; UTC, maximal resolution 1/10 of second
    /// </summary>
    public string? Timestamp { get; init; }
}

public record SelfIdData
{
    /// <summary>
    /// TEXT = 0, EMERGENCY = 1, EXTENDED STATUS = 2
    /// </summary>
    public int DescType { get; init; }
    public string? Desc { get; init; }
}

public record SystemData
{
    /// <summary>
    /// TAKEOFF = 0, LIVE_GNSS = 1, FIXED = 2
    /// </summary>
    public int OperatorLocationType { get; init; }

    /// <summary>
    /// UNDECLARED = 0, EU = 1
    /// </summary>
    public int ClassificationType { get; init; }

    /// <summary>
    /// -90+90; 7 decimal places
    /// </summary>
    public float? OperatorLatitude { get; init; }

    /// <summary>
    /// -180-+180; 7 decimal places
    /// </summary>
    public float? OperatorLongitude { get; init; }

    /// <summary>
    /// quantity in a swarm; default 1
    /// </summary>
    public int AreaCount { get; init; }

    /// <summary>
    /// meters, farthest horizontal distance from any UA's position in a group
    /// </summary>
    public int AreaRadius { get; init; }

    /// <summary>
    /// meters, can be negative, maximal altitude of a swarm
    /// </summary>
    public float? AreaCeiling { get; init; }

    /// <summary>
    /// meters, can be negative, minimal altitude of a swarm
    /// </summary>
    public float? AreaFloor { get; init; }

    /// <summary>
    /// UNDECLARED = 0, OPEN = 1, SPECIFIC = 2, CERTIFIED = 3
    /// </summary>
    public int CategoryEu { get; init; }

    /// <summary>
    /// UNDECLARED = 0, CLASS_0=1...CLASS_6 = 7
    /// </summary>
    public int ClassEu { get; init; }

    /// <summary>
    /// meters (WGS84-HAE)
    /// </summary>
    public float? OperatorAltitudeGeo { get; init; }

    /// <summary>
    /// date-time in ISO 8601 format; UTC, resolution 1 second
    /// </summary>
    public string? Timestamp { get; init; }
}

public record OperatorIdData
{
    /// <summary>
    /// OPERATOR ID=0
    /// </summary>
    public int OperatorIdType { get; init; }
    public string? OperatorId { get; init; }
}