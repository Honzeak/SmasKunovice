import json
import sys

def filter_and_parse_dronetag_odid_messages(json_file_path):
    """
    Filters log messages from a JSON file, keeping only those with
    "topic": "dronetag/odid". For each filtered message, it extracts
    and parses the embedded JSON string from the 'properties.payload' field.

    Args:
        json_file_path (str): The path to the input JSON file.

    Returns:
        list: A list of parsed payload dictionaries from the filtered messages.
    """
    try:
        with open(json_file_path, 'r', encoding='utf-8') as f:
            # Load the entire JSON content. Assuming it's a list of message objects.
            source = json.load(f)

        log_messages = source[0].get('messages')
        if not isinstance(log_messages, list):
            print(f"Error: The JSON file '{json_file_path}' does not contain a list of messages.", file=sys.stderr)
            return []

        parsed_payloads = []
        for message in log_messages:
            # Check if the 'topic' key exists and its value is 'dronetag/odid'
            if message.get('topic') == 'dronetag/odid':
                # Ensure 'properties' and 'payload' exist before trying to access
                if 'properties' in message and 'payload' in message['properties']:
                    payload_string = message['properties']['payload']
                    try:
                        # Attempt to parse the embedded JSON string
                        parsed_payload = json.loads(payload_string)
                        parsed_payloads.append(parsed_payload)
                    except json.JSONDecodeError as e:
                        print(f"Warning: Could not decode embedded JSON in payload for message ID '{message.get('id', 'N/A')}': {e}", file=sys.stderr)
                else:
                    print(f"Warning: Message ID '{message.get('id', 'N/A')}' with topic 'dronetag/odid' is missing 'properties' or 'properties.payload'. Skipping.", file=sys.stderr)
        return parsed_payloads

    except FileNotFoundError:
        print(f"Error: File not found at '{json_file_path}'", file=sys.stderr)
        return []
    except json.JSONDecodeError:
        print(f"Error: Could not decode JSON from '{json_file_path}'. Please ensure it's valid JSON.", file=sys.stderr)
        return []
    except Exception as e:
        print(f"An unexpected error occurred: {e}", file=sys.stderr)
        return []

if __name__ == "__main__":
    # Check if correct number of arguments are provided
    if len(sys.argv) < 3:
        print("Usage: python filter_logs.py <path_to_input_json_file> <path_to_output_json_file>", file=sys.stderr)
        sys.exit(1)

    input_file = sys.argv[1]
    output_file = sys.argv[2]
    
    # Filter and parse the messages
    parsed_data = filter_and_parse_dronetag_odid_messages(input_file)

    # Write the parsed data to the specified output file
    if parsed_data:
        try:
            with open(output_file, 'w', encoding='utf-8') as outfile:
                outfile.write(json.dumps(parsed_data, indent=4))
            print(f"Parsed payload data successfully written to '{output_file}'")
        except IOError as e:
            print(f"Error: Could not write to output file '{output_file}': {e}", file=sys.stderr)
    else:
        print(f"No messages found with topic 'dronetag/odid' and valid embedded payloads in '{input_file}', or an error occurred. No output file written.")

