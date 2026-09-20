import json
import math
import random
import argparse
import copy
from datetime import datetime, timedelta

def calculate_distance(lat1, lon1, lat2, lon2):
    """Calculate distance in meters between two coordinates using the Haversine formula."""
    R = 6371000  # Radius of the Earth in meters
    phi1 = math.radians(lat1)
    phi2 = math.radians(lat2)
    delta_phi = math.radians(lat2 - lat1)
    delta_lambda = math.radians(lon2 - lon1)
    
    a = math.sin(delta_phi / 2.0)**2 + \
        math.cos(phi1) * math.cos(phi2) * \
        math.sin(delta_lambda / 2.0)**2
    c = 2 * math.atan2(math.sqrt(a), math.sqrt(1 - a))
    return R * c

def calculate_bearing(lat1, lon1, lat2, lon2):
    """Calculate the direction/bearing [0-360] from the first point to the second."""
    lat1, lon1 = math.radians(lat1), math.radians(lon1)
    lat2, lon2 = math.radians(lat2), math.radians(lon2)
    
    dlon = lon2 - lon1
    x = math.sin(dlon) * math.cos(lat2)
    y = math.cos(lat1) * math.sin(lat2) - (math.sin(lat1) * math.cos(lat2) * math.cos(dlon))
    
    initial_bearing = math.atan2(x, y)
    initial_bearing = math.degrees(initial_bearing)
    return (initial_bearing + 360) % 360

def generate_random_uasid():
    """Generate a random 6-character hexadecimal UASID."""
    return f"{random.randint(0, 0xFFFFFF):06X}"

def main():
    parser = argparse.ArgumentParser(description="Convert GeoJSON to ODID log file.")
    parser.add_argument("-i", "--input", required=True, help="Input GeoJSON file")
    parser.add_argument("-o", "--output", required=True, help="Output JSON log file")
    parser.add_argument("-a", "--altitude", type=float, required=True, help="AltitudeBaro in meters")
    
    args = parser.parse_args()
    
    with open(args.input, "r") as f:
        geojson_data = json.load(f)
        
    features = geojson_data.get("features", [])
    
    base_msg = {
        "sn": "D17D80CF73E53E5DF9",
        "mac": "00:00:00:00:00:00",
        "counter": 0,
        "rssi": -65,
        "tech": "AB",
        "odid": {
            "BasicID": [
                {
                    "UAType": 16,
                    "IDType": 2,
                    "UASID": ""
                }
            ],
            "Location": {
                "Status": 2,
                "Direction": 0.0,
                "SpeedHorizontal": 0.0,
                "SpeedVertical": 0.0,
                "Latitude": 0.0,
                "Longitude": 0.0,
                "AltitudeBaro": args.altitude,
                "AltitudeGeo": args.altitude,
                "HeightType": 0,
                "Height": None,
                "HorizAccuracy": 0,
                "VertAccuracy": 0,
                "BaroAccuracy": 0,
                "SpeedAccuracy": 0,
                "TSAccuracy": 0,
                "Timestamp": "",
                "TimestampEstimated": False
            },
            "SelfID": {
                "DescType": 0,
                "Desc": "AIRCRAFT"
            },
            "System": None,
            "OperatorID": None
        },
        "recv_id": 0,
        "module_id": 0,
        "module_type": 1090,
        "msg_type": 15,
        "noise_floor": None,
        "registration": None
    }
    
    aircraft_states = {}
    output_log = []
    
    script_start_time = datetime.now()
    
    for feature in features:
        if feature.get("geometry", {}).get("type") != "Point":
            continue
            
        coords = feature["geometry"]["coordinates"]
        lon, lat = coords[0], coords[1]
        ac_id = feature.get("properties", {}).get("id")
        
        # Initialize new aircraft state with +500 ms offset per aircraft
        if ac_id not in aircraft_states:
            ac_index = len(aircraft_states)
            ac_base_time = script_start_time + timedelta(milliseconds=500 * ac_index)
            
            uasid = generate_random_uasid()
            mac_address = f"00:00:{uasid[0:2].lower()}:{uasid[2:4].lower()}:{uasid[4:6].lower()}:00"
            
            aircraft_states[ac_id] = {
                "uasid": uasid,
                "mac": mac_address,
                "desc": f"AC_ID_{ac_id}",
                "last_lat": lat,
                "last_lon": lon,
                "last_time": ac_base_time,
                "last_direction": 0.0,
                "is_first_point": True
            }
            
        state = aircraft_states[ac_id]
        msg = copy.deepcopy(base_msg)
        
        # Identity
        msg["mac"] = state["mac"]
        msg["odid"]["BasicID"][0]["UASID"] = state["uasid"]
        msg["odid"]["SelfID"]["Desc"] = state["desc"]
        
        # Time and telemetry calculation
        if state["is_first_point"]:
            current_time = state["last_time"]
            speed = 0.0
            direction = 0.0
            state["is_first_point"] = False
        else:
            current_time = state["last_time"] + timedelta(seconds=3)
            distance = calculate_distance(state["last_lat"], state["last_lon"], lat, lon)
            speed = distance / 3.0
            
            if distance > 0:
                direction = calculate_bearing(state["last_lat"], state["last_lon"], lat, lon)
                state["last_direction"] = direction
            else:
                direction = state["last_direction"]
                
        formatted_time = current_time.strftime("%Y-%m-%dT%H:%M:%S.%f")
        
        # Inject telemetry
        msg["odid"]["Location"]["Latitude"] = lat
        msg["odid"]["Location"]["Longitude"] = lon
        msg["odid"]["Location"]["Timestamp"] = formatted_time
        msg["odid"]["Location"]["SpeedHorizontal"] = speed
        msg["odid"]["Location"]["Direction"] = direction
        
        # Advance state
        state["last_lat"] = lat
        state["last_lon"] = lon
        state["last_time"] = current_time
        
        output_log.append(msg)
        
    # Sort messages chronologically by timestamp
    output_log.sort(key=lambda item: item["odid"]["Location"]["Timestamp"])
    
    # Save output log: Join each 1-item array with a comma and newline
    with open(args.output, "w") as f:
        formatted_messages = [json.dumps([msg], indent=2) for msg in output_log]
        f.write(",\n".join(formatted_messages) + "\n")
            
    print(f"Successfully processed {len(output_log)} points for {len(aircraft_states)} aircraft.")
    print(f"Comma-separated array stream saved to {args.output}")

if __name__ == "__main__":
    main()