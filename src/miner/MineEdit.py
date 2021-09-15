#!/usr/bin/env python

import argparse
import os
import subprocess
import sys
import git
from github import Github
import pathlib
import utils


script_path = os.path.dirname(os.path.realpath(__file__))


def get_relevant_client(lib_name, old, new):
    relevant_clients = []
    mine_client_edits_path = os.path.join(script_path, "..", "miner", "result", lib_name+"_result.json")
    if not os.path.exists(mine_client_edits_path):
        utils.log("[ERROR] the client info file does not exist. Please first run the crawler.py by disabling only-latest!!!")
        exit(1)
    with open(mine_client_edits_path) as f:
        mine_client_edits_json = json.load(f)
        for cl in mine_client_edits_json:
            # if not is_version_greater(old, cl["old_version"]) and not is_version_greater(cl["new_version"], new):
            #if is_version_greater(cl["old_version"], new) and not is_version_greater(cl["new_version"], new):
            if not utils.is_version_greater(old, cl["old_version"]) and utils.is_version_greater(old, cl["new_version"]):
            	relevant_clients.append(cl)

    return relevant_clients


def get_repo(url, path, commit_id):
    if not os.path.exists(path):
        try:
            url = url.replace("github", ":@github")
            git.Repo.clone_from(url, path)
        except:
            return 1
    try:
        g = git.cmd.Git(path)
        g.checkout("-f", commit_id)
    except:
        return 1

    return 0


def invoke_csharpengine(lib_name, old_version, new_version, client_name, old_client_version, new_client_version, sln_path):
    csharp_engine_path = os.path.join(script_path, "..", "CSharpEngine", "bin", "Debug", "net5.0", "CSharpEngine.exe")
    command = csharp_engine_path + \
              " -l " + lib_name + \
              " -m " + old_version + \
              " -n " + new_version + \
              " -c " + client_name + \
              " -s " + old_client_version + \
              " -t " + new_client_version + \
              " -f " + script_path, CONFIGURATION_FILE
    if COMPILATION_MODE:
        command = command + " -y -p " + sln_path
    utils.log("[INFO] command: " + command)
    return_code = subprocess.call(command, shell=True)
    if return_code != 0:
        command = "rm -rf " + old_client_version
        subprocess.call(command, shell=True)
        command = "rm -rf " + new_client_version
        subprocess.call(command, shell=True)
        #utils.log("failed to invoke CSharpEngine")
        #exit(return_code)


def invoke_csharpengine_to_mine_itself(lib_name, old_version, new_version):
    csharp_engine_path = os.path.join(script_path, "..", "CSharpEngine", "bin", "Debug", "net5.0", "CSharpEngine.exe")
    command = csharp_engine_path + \
              " -l " + lib_name + \
              " -m " + old_version + \
              " -n " + new_version + \
              " -f " + CONFIGURATION_FILE + \
              " -i"
    if COMPILATION_MODE:
        sln_file_list_new = utils.pre_build(lib_name, lib_name, new_version)
        sln_file_list_old = utils.pre_build(lib_name, lib_name, old_version)

        for sln_file in sln_file_list_new:        
            command = command + " -y -p " + sln_file
            utils.log("[COMMAND]: " + str(command))
            return_code = subprocess.call(command, shell=True)
            utils.log("[INFO] Ret Code: " + str(return_code))
            if return_code == 0:
                break
    else:
        utils.log("[INFO] command: " + command)
        return_code = subprocess.call(command, shell=True)

    if return_code != 0:
        if str(return_code) == "1":
            utils.log("[ERROR] invocation of CSharpEngine failed with return code: " + str(return_code))
        else:
            utils.log("[ERROR] invocation of CSharpEngine crashed with return code: " + str(return_code))
    else:
        utils.log("[INFO] mining human adaptations from library itself is done!!!")
    return return_code


def extract_sln_path(client_path):
    sln_paths = list(pathlib.Path(client_path).rglob('*.sln'))
    if len(sln_paths) != 0:
        utils.log(client_path, sln_paths[0])
        sln_path = sln_paths[0][len(client_path):]
        return sln_path
    return None


def trim(version):
    return ''.join( c for c in version if c in '1234567890.' )


def mine_library_edits(author_name, lib_name, old_lib_version, new_lib_version):
    library_path = os.path.join(script_path, "..", "..", "benchmark", lib_name, "library")
    if not os.path.exists(library_path):
        os.makedirs(library_path, exist_ok=True)
    library_link = "https://github.com/" + author_name + "/" + lib_name

    old_lib_version_id = trim(old_lib_version)
    new_lib_version_id = trim(new_lib_version)
    old_library_path = os.path.join(library_path, lib_name + "-" + old_lib_version_id)
    new_library_path = os.path.join(library_path, lib_name + "-" + new_lib_version_id)

    utils.log("[INFO] cloning library repo ...")
    ret1 = get_repo(library_link, old_library_path, old_lib_version)
    ret2 = get_repo(library_link, new_library_path, new_lib_version)
    if ret1 != 0 or ret2 != 0:
        utils.log("[ERROR] Failed to clone the library repo ...")
        return

    utils.log("[INFO] minging human adaptation from library itself ...")
    invoke_csharpengine_to_mine_itself(lib_name, old_lib_version_id, new_lib_version_id)


def mine_client_edits(lib_name, client, old_lib_version, new_lib_version):
    # utils.log(client)
    client_path = os.path.join(script_path, "..", "..", "benchmark", lib_name, "client")
    if not os.path.exists(client_path):
        os.makedirs(client_path, exist_ok=True)

    client_name = client["client_name"]
    old_commit_id = client["old_commit_id"]
    # new_commit_id = client["new_commit_id"]
    new_commit_id = client["five_day_commit"]
    old_client_path = os.path.join(client_path, client_name + "-" + old_commit_id[0:6])
    new_client_path = os.path.join(client_path, client_name + "-" + new_commit_id[0:6])
    ret1 = get_repo(client["html_url"], old_client_path, old_commit_id)
    ret2 = get_repo(client["html_url"], new_client_path, new_commit_id)
    if ret1 != 0 or ret2 != 0:
        return

    sln_path = None
    invoke_csharpengine(lib_name, old_lib_version, new_lib_version, client_name, old_commit_id[0:6], new_commit_id[0:6], sln_path)


skip_clients = ["app-innovation-workshop", "xplat-netcore-webassembly", "das-payments-V2_c2a4bf", "RecordPoint.Connectors.SDK", "das-payments-V2"]


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description='Mine humam adaptations for breaking changes')
    parser.add_argument(dest='author', type=str, help='the author name of the library')
    parser.add_argument(dest='library', type=str, help='the name of the library')
    parser.add_argument('-f', '--config_file', dest='config_file', help='the path to the configuration file', required=True)
    parser.add_argument("--only-mine-library", dest="mine_library", action="store_true", 
                        help="mine the human adaptations from library itself")
    parser.add_argument('--compilation-mode', dest='compilation', action="store_true",
                        help='mine human edit in compilation mode', required=False)
    args = parser.parse_args()
    
    COMPILATION_MODE = args.compilation
    CONFIGURATION_FILE = args.config_file
    if not os.path.isfile(CONFIGURATION_FILE):
        utils.log("[ERROR] The configuration file does not exist!!!")
        exit(1)
    target_apis = utils.read_json(CONFIGURATION_FILE)
    if target_apis["library"] != args.library:
        utils.log("[ERROR] Please provide correct configuration file!!!")
        exit(1)

    old_lib_version = target_apis["source"]
    new_lib_version = target_apis["target"]

    mine_library_edits(args.author, args.library, old_lib_version, new_lib_version)
    
    old_lib_version_id = trim(old_lib_version)
    new_lib_version_id = trim(new_lib_version)
    if not args.mine_library:
        relevant_client_edits = get_relevant_client(args.library, old_lib_version_id, new_lib_version_id)
    
        length = len(relevant_client_edits)
        utils.log("the number of relevant clients is ", length)
    
        for i in range(length):
            client = relevant_client_edits[i]
            if client["client_name"] in skip_clients:
                continue
            mine_client_edits(args.library, client, old_lib_version_id, new_lib_version_id)

            progress = int(float(100*i) / (length-1) / 2)
            sys.stdout.write('\r')
            sys.stdout.write("[%-50s] %d%%" % ('='*progress, 2*progress))
            sys.stdout.flush()
        utils.log()
