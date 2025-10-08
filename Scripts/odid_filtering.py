import json
import argparse
import sys
import os

def parse_concatenated_json(input_filepath, output_filepath):
    """
    Parses a file containing concatenated JSON objects (without commas) 
    by counting braces and using a string state machine to correctly 
    delimit each complex object, then saves them to an output file 
    as a valid JSON array.
    """
    print(f"--- Starting JSON Parsing ---")
    print(f"Input file: {input_filepath}")
    
    if not os.path.exists(input_filepath):
        print(f"Error: Input file not found at '{input_filepath}'")
        return

    brace_count = 0
    start_index = 0
    messages = []
    in_string = False
    
    try:
        # Read the entire file content. For gigabyte-sized files, this is often
        # faster than character-by-character reading from disk, but be mindful
        # of memory constraints for truly enormous (TB+) files.
        with open(input_filepath, 'r', encoding='utf-8') as f:
            content = f.read()
    except Exception as e:
        print(f"An error occurred while reading the file: {e}")
        return

    print(f"Content loaded. Total size: {len(content)} bytes.")

    # Iterate over the content character by character
    for i, char in enumerate(content):
        # 1. State machine to handle quoted strings (which might contain braces)
        if char == '"':
            # Toggle the in_string flag, ignoring escaped quotes inside strings
            # Note: This simple check works for non-nested escaped quotes.
            if i == 0 or content[i-1] != '\\':
                in_string = not in_string
        
        # 2. Brace Counting (only if not inside a string literal)
        if not in_string:
            if char == '{':
                if brace_count == 0:
                    # Mark the start of a potential new object
                    start_index = i
                brace_count += 1
            elif char == '}':
                brace_count -= 1
                
                # Check for the end of a top-level object
                if brace_count == 0:
                    # Found a complete, top-level JSON object
                    json_string = content[start_index : i + 1]
                    
                    try:
                        # Attempt to parse the extracted string
                        message = json.loads(json_string)
                        messages.append(message)
                    except json.JSONDecodeError as e:
                        print(f"Warning: Failed to decode JSON starting at index {start_index}. Skipping. Error: {e}")
    
    # Check for unmatched braces at the end of file (incomplete message)
    if brace_count != 0:
        print(f"Warning: Reached end of file with unbalanced braces (count: {brace_count}). Last message may be incomplete.")

    print(f"Successfully parsed {len(messages)} JSON objects.")
    
    # 3. Write the results
    print(f"Writing repaired JSON to: {output_filepath}...")
    try:
        with open(output_filepath, 'w', encoding='utf-8') as f:
            # Use json.dump for clean, valid JSON output
            json.dump(messages, f, indent=2)
        print("--- Repair complete! ---")
        print(f"Output saved to '{output_filepath}'.")
        
    except Exception as e:
        print(f"An error occurred while writing the file: {e}")


def main():
    """Main function to handle command-line arguments and call the parser."""
    parser = argparse.ArgumentParser(
        description="Repair log files with concatenated JSON objects by counting braces."
    )
    
    parser.add_argument(
        '-i', '--input', 
        type=str, 
        required=True, 
        help="Path to the input log file containing concatenated JSON messages."
    )
    
    parser.add_argument(
        '-o', '--output', 
        type=str, 
        required=True, 
        help="Path to the output file where the repaired JSON array will be saved."
    )
    
    args = parser.parse_args()
    
    # Call the main parsing logic
    parse_concatenated_json(args.input, args.output)

if __name__ == '__main__':
    main()
