import re
import json


class UpdateItem:

    def __init__(self, html_url, old_version, new_version, old_commit_id, new_commit_id, client_name, five_day_commit):
        self.html_url = html_url
        self.old_version = old_version
        self.new_version = new_version
        self.old_commit_id = old_commit_id
        self.new_commit_id = new_commit_id
        self.client_name = client_name
        self.five_day_commit = five_day_commit

    def to_json(self):
        return json.dumps(self, default=lambda o: o.__dict__,
                          sort_keys=False, indent=4)


def dumper(obj):
    try:
        return obj.to_json()
    except:
        return obj.__dict__


def extract_data(library_name, file_path):
    mine_results = open(file_path, "r")
    lines = mine_results.readlines()
    mine_results.close()
    for line in lines:
        if line == "" or line is None:
            continue
        if re.match("https://github.com/.*/.*/[0-9a-fA-F]{40}", line) is not None:
            print("\n" + line[:-1])
            continue
        elif "."+library_name in line or library_name + "." in line:
            continue
        versions = extract_version(line)
        if len(versions) > 0:
            print(versions[0])


def extract_commit_id(line):
    ids = re.match("https://github.com/.*/.*/([0-9a-fA-F]{40})", line, re.S)
    if ids:
        return ids.group(1)


def extract_version(line):
    extracted_data = ""
    within_bracket = False
    for ch in line:
        if ch == '<':
            within_bracket = True
        elif ch == '>':
            within_bracket = False
        elif within_bracket is False:
            extracted_data += ch
    versions = re.findall("\d+[\.\d+]+", extracted_data)
    if versions is not None and len(versions) > 0:
        return versions[0]
    else:
        return  None


# extract_data("Polly", "Polly_result.txt")
