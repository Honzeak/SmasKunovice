using System.Text.Json;

namespace SmasKunovice.Avalonia.Models;

public class ScoutData
{
    public static JsonSerializerOptions SerializerOptions { get; } = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        Converters = { new StringOrStringArrayConverter() }
    };

    /// <summary>
    /// Reported RSSI by the receiving module
    /// </summary>
    public int Rssi { get; set; }

    /// <summary>
    /// Receiving technology: B4 (Bluetooth legacy), B5 (Bluetooth LE), WN (Wi-Fi Nan), WB (Wi-Fi Beacon)
    /// </summary>
    public string[]? Tech { get; set; }

    /// <summary>
    /// Module number that received the message. Corresponds to the antenna position on the box.
    /// </summary>
    public int RecvId { get; set; }

    /// <summary>
    /// Module type that received the message. Mainly for internal use.
    /// </summary>
    public int ModuleId { get; set; }

    /// <summary>
    /// ODID message type as defined in the reference: BASIC_ID (0), LOCATION (1), AUTH (2), SELF_ID (3),
    /// SYSTEM (4), OPERATOR_ID (5), PACKED (15), INVALID (255); we don't forward AUTH messages
    /// </summary>
    public int MsgType { get; set; }
    public OdidData? Odid { get; set; }
}

public class OdidData
{
    public BasicIdData[]? BasicId { get; set; }

    /// <summary>
    /// see table 1.3 Position of the aircraft. This will be the most common message over B4. Other techs use
    /// mainly PACKED messages with location included. Used units: M - meters, M/S - meters per
    /// second, NM - nautical miles (1.852 km).
    /// </summary>
    public LocationData? Location { get; set; }

    /// <summary>
    /// see table 1.4 The Self-ID Message is optionally sent in case the Remote Pilot wishes to declare its
    /// identity, flight purpose or both. This may serve as mitigation of a perceived threat by a
    /// neighbouring person or public in case a UA is operating in the same close area.
    /// </summary>
    public SelfIdData? SelfId { get; set; }

    /// <summary>
    /// see table 1.5 Contains information about the Remote Pilot location and a swarm (if applicable).
    /// </summary>
    public SystemData? System { get; set; }

    /// <summary>
    /// see table 1.6 UAS Operator Registration Number.
    /// </summary>
    public OperatorIdData? OperatorId { get; set; }
}
// Class to hold the JSON/ODID data structure, mirroring the document

public class BasicIdData
{
    /// <summary>
    /// UNKNOWN = 0. AEROPLANE = 1, HELICOPTER_OR_MULTIROTOR = 2, GYROPLANE = 3, HYBRID_LIFT = 4,
    /// ORNITHOPTER = 5, GLIDER = 6, KITE = 7 FREE_BALLOON = 8, CAPTIVE_BALLOON = 9, AIRSHIP = 10,
    /// FREE_FALL_PARACHUTE = 11, ROCKET = 12, TETHERED_POWERED_AIRCRAFT = 13, GROUND_OBSTACLE = 14, OTHER = 15
    /// </summary>
    public int UaType { get; set; }

    /// <summary>
    /// NONE = 0, SERIAL_NUMBER = 1, CAA_REGISTRATION_ID = 2, UTM_ASSIGNED_UUID = 3, SPECIFIC_SESSION_ID = 4
    /// </summary>
    public int IdType { get; set; }

    /// <summary>
    /// e.g.: 1596F350457791312042
    /// </summary>
    public string? UasId { get; set; }
}

public class LocationData
{
    /// <summary>
    /// UNDECLARED = 0, GROUND = 1, AIRBORNE = 2, EMERGENCY = 3, REMOTE_ID_SYSTEM_FAILURE = 4
    /// </summary>
    public int Status { get; set; }

    /// <summary>
    /// 0-360 degrees
    /// </summary>
    public int? Direction { get; set; }

    /// <summary>
    /// 0.0-255.0 m/s.
    /// Positive only. Invalid, if speed is >= 254.25 m/s: 254.25m/s
    /// </summary>
    public float? SpeedHorizontal { get; set; }

    /// <summary>
    /// m/s. Invalid, No Value, or Unknown: 63m/s. If speed is >= 62m/s: 62m/s
    /// </summary>
    public float? SpeedVertical { get; set; }

    /// <summary>
    /// -180-+180; 7 decimal places
    /// </summary>
    public float? Longitude { get; set; }

    /// <summary>
    /// -90- +90; 7 decimal places
    /// </summary>
    public float? Latitude { get; set; }

    /// <summary>
    /// meter (Ref 29.92 inHg, 1013.24 mb)
    /// </summary>
    public float? AltitudeBaro { get; set; }

    /// <summary>
    /// meter (WGS84-HAE)
    /// </summary>
    public float? AltitudeGeo { get; set; }

    /// <summary>
    /// OVER_TAKEOFF = 0, OVER_GROUND = 1
    /// </summary>
    public int HeightType { get; set; }

    /// <summary>
    /// meters; can be negative
    /// </summary>
    public float? Height { get; set; }

    /// <summary>
    /// UNKNOWN=0, 10NM=1, 4NM=2, 2NM=3, 1NM=4, 5NM=5.
    /// 0_3NM=6, 0_ 1 NM=7 05NM=8, 30M=9, 10M=10, 3M=11, 1M=12
    /// </summary>
    public int HorizAccuracy { get; set; }

    /// <summary>
    /// UNKNOWN = 0, 150M = 1, 45M = 2, 25M = 3, 10M = 4, 3M = 5, 1M = 6
    /// </summary>
    public int VertAccuracy { get; set; }

    /// <summary>
    /// the same as VertAccuracy
    /// </summary>
    public int BaroAccuracy { get; set; }

    /// <summary>
    /// UNKNOWN=0, 10M/S=1, 3M/S=2, 1 M/S=3, 0.3M/S=4
    /// </summary>
    public int SpeedAccuracy { get; set; }

    /// <summary>
    /// UNKNOWN=0, 0.1s=1, 0.2s=2, 0.3s=3, 0.4s=4, 0.5s=5, 0.6s=6,
    /// 0.7s=7, 0.8s=8, 0.9s=9, 1.0s=10, 1.1s=11, 1.2s=12, 1.3s=13, 1.4s=14,
    /// 1.5s=15
    /// </summary>
    public int TsAccuracy { get; set; }

    /// <summary>
    /// date-time in ISO 8601 format; UTC, maximal resolution 1/10 of second
    /// </summary>
    public string? Timestamp { get; set; }
}

public class SelfIdData
{
    /// <summary>
    /// TEXT = 0, EMERGENCY = 1, EXTENDED STATUS = 2
    /// </summary>
    public int DescType { get; set; }
    public string? Desc { get; set; }
}

public class SystemData
{
    /// <summary>
    /// TAKEOFF = 0, LIVE_GNSS = 1, FIXED = 2
    /// </summary>
    public int OperatorLocationType { get; set; }

    /// <summary>
    /// UNDECLARED = 0, EU = 1
    /// </summary>
    public int ClassificationType { get; set; }

    /// <summary>
    /// -90+90; 7 decimal places
    /// </summary>
    public float? OperatorLatitude { get; set; }

    /// <summary>
    /// -180-+180; 7 decimal places
    /// </summary>
    public float? OperatorLongitude { get; set; }

    /// <summary>
    /// quantity in a swarm; default 1
    /// </summary>
    public int AreaCount { get; set; }

    /// <summary>
    /// meters, farthest horizontal distance from any UA's position in a group
    /// </summary>
    public int AreaRadius { get; set; }

    /// <summary>
    /// meters, can be negative, maximal altitude of a swarm
    /// </summary>
    public float? AreaCeiling { get; set; }

    /// <summary>
    /// meters, can be negative, minimal altitude of a swarm
    /// </summary>
    public float? AreaFloor { get; set; }

    /// <summary>
    /// UNDECLARED = 0, OPEN = 1, SPECIFIC = 2, CERTIFIED = 3
    /// </summary>
    public int CategoryEu { get; set; }

    /// <summary>
    /// UNDECLARED = 0, CLASS_0=1...CLASS_6 = 7
    /// </summary>
    public int ClassEu { get; set; }

    /// <summary>
    /// meters (WGS84-HAE)
    /// </summary>
    public float? OperatorAltitudeGeo { get; set; }

    /// <summary>
    /// date-time in ISO 8601 format; UTC, resolution 1 second
    /// </summary>
    public string? Timestamp { get; set; }
}

public class OperatorIdData
{
    /// <summary>
    /// OPERATOR ID=0
    /// </summary>
    public int OperatorIdType { get; set; }
    public string? OperatorId { get; set; }
}