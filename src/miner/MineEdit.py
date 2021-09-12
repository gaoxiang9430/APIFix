#!/usr/bin/env python

import argparse
import os
import json
import subprocess
import sys
import git
from github import Github
import pathlib


script_path = os.path.dirname(os.path.realpath(__file__))


def get_client_update(lib_name, old, new):
    relevant_clients = []
    client_info_path = os.path.join(script_path, "..", "miner", "result", lib_name+"_result.json")
    if not os.path.exists(client_info_path):
        print("the client info file does not exist!!!")
        exit(1)
    with open(client_info_path) as f:
        client_info_json = json.load(f)
        for cl in client_info_json:
            # if not is_version_greater(old, cl["old_version"]) and not is_version_greater(cl["new_version"], new):
            #if is_version_greater(cl["old_version"], new) and not is_version_greater(cl["new_version"], new):
            if not is_version_greater(old, cl["old_version"]) and is_version_greater(old, cl["new_version"]):
            	relevant_clients.append(cl)

    return relevant_clients


def is_version_greater(base_version, comp_version):
    if base_version == "" or comp_version=="":
        return False
    if "." in base_version:
        base_version_digit = int(base_version.split(".")[0])
    else:
        base_version_digit = int(base_version)
    if "." in comp_version:
        comp_version_digit = int(comp_version.split(".")[0])
    else:
        comp_version_digit = int(comp_version)

    if base_version_digit < comp_version_digit:
        return True
    elif base_version_digit == comp_version_digit:
        base_sub_version_digit_list = base_version.split(".")[1:]
        comp_sub_version_digit_list = comp_version.split(".")[1:]
        if not base_sub_version_digit_list and comp_sub_version_digit_list:
            return True
        if not comp_sub_version_digit_list and base_sub_version_digit_list:
            return False
        base_sub_version = ".".join(base_sub_version_digit_list)
        comp_sub_version = ".".join(comp_sub_version_digit_list)
        return is_version_greater(base_sub_version, comp_sub_version)
    else:
        return False


def get_repo(url, client_path, commit_id):
    if not os.path.exists(client_path):
        try:
            url = url.replace("github", ":@github")
            git.Repo.clone_from(url, client_path)
        except:
            return 1

    try:
        g = git.cmd.Git(client_path)
        g.checkout("-f", commit_id)
    except:
        return 1

    return 0


def invoke_csharpengine(lib_name, old_version, new_version, client_name, old_client_version, new_client_version, sln_path):
    csharp_engine_path = os.path.join(script_path, "..", "CSharpEngine", "bin", "Debug", "net5.0", "CSharpEngine.exe")
    command = csharp_engine_path + " -l " + lib_name + " -m " + old_version + " -n " + new_version + " -c " \
              + client_name + " -s " + old_client_version + " -t  " + new_client_version
    if sln_path != None:
        command = command + " -p " + sln_path
    print("command: " + command)
    return_code = subprocess.call(command, shell=True)
    if return_code != 0:
        command = "rm -rf " + old_client_version
        subprocess.call(command, shell=True)
        command = "rm -rf " + new_client_version
        subprocess.call(command, shell=True)
        #print("failed to invoke CSharpEngine")
        #exit(return_code)

def extract_sln_path(client_path):
    sln_paths = list(pathlib.Path(client_path).rglob('*.sln'))
    if len(sln_paths) != 0:
        print(client_path, sln_paths[0])
        sln_path = sln_paths[0][len(client_path):]
        return sln_path
    return None


def extract_edits(lib_name, client_info, old_lib_version, new_lib_version):
    # print(client_info)
    client_path = os.path.join(script_path, "..", "..", "benchmark", lib_name, "client")
    if not os.path.exists(client_path):
        os.mkdir(client_path)

    client_name = client_info["client_name"]
    old_commit_id = client_info["old_commit_id"]
    # new_commit_id = client_info["new_commit_id"]
    new_commit_id = client_info["five_day_commit"]
    old_client_path = os.path.join(client_path, client_name + "-" + old_commit_id[0:6])
    new_client_path = os.path.join(client_path, client_name + "-" + new_commit_id[0:6])
    ret1 = get_repo(client_info["html_url"], old_client_path, old_commit_id)
    ret2 = get_repo(client_info["html_url"], new_client_path, new_commit_id)
    if ret1 != 0 or ret2 != 0:
        return

    # find SLN path
    #sln_path = extract_sln_path(client_path)
    #if sln_path == None:
    #    return
    sln_path = None
    invoke_csharpengine(lib_name, old_lib_version, new_lib_version, client_name, old_commit_id[0:6], new_commit_id[0:6], sln_path)


skip_clients = ["app-innovation-workshop", "xplat-netcore-webassembly", "das-payments-V2_c2a4bf", "RecordPoint.Connectors.SDK", "das-payments-V2"]


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description='Breaking-changes')
    parser.add_argument(dest='name', type=str, help='the name of the library')
    parser.add_argument('-o', '--old', dest='old', type=str,
                        help='the old library version', required=True)
    parser.add_argument('-n', '--new', dest='new', type=str,
                        help='the new library versions', required=True)
    args = parser.parse_args()

    relevant_client_edits = get_client_update(args.name, args.old, args.new)
    
    length = len(relevant_client_edits)
    print("the number of relevant clients is ", length)
    
    for i in range(length):
        client = relevant_client_edits[i]
        if client["client_name"] in skip_clients:
            continue
        extract_edits(args.name, client, args.old, args.new)

        progress = int(float(100*i) / (length-1) / 2)
        sys.stdout.write('\r')
        sys.stdout.write("[%-50s] %d%%" % ('='*progress, 2*progress))
        sys.stdout.flush()
    print()
