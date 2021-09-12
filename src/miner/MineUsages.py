#!/usr/bin/env python

import argparse
import os
import shutil
import json
import datetime
import subprocess
import sys
import git
import time
from github import Github
from alive_progress import alive_bar

DIRECTORY_ROOT = "/".join(os.path.realpath(__file__).split("/")[:-2])
DIRECTORY_LOG = DIRECTORY_ROOT + "/logs"
ACCESS_TOKEN = "66f7a4e70b3948b8665dc78b8a722e017c0a7e24"
FILE_MAIN_LOG = "temp.log"
FILE_STATIC_CLIENT_LIST = ""
FILE_TYPED_CLIENT_LIST = ""

ONLY_COMPILATION_MODE = False
ONLY_NEW = False
ONLY_OLD = False

script_path = os.path.dirname(os.path.realpath(__file__))
g = Github(ACCESS_TOKEN)
FILE_API_LIST = script_path + "/../../benchmark/interesting_api.json"


def read_json(file_path):
    with open(file_path, 'r', encoding='utf-8', errors='ignore') as in_file:
        json_data = json.load(in_file)
        return json_data


def log(log_message):
    global FILE_MAIN_LOG
    log_message = "[" + str(time.asctime()) + "]" + log_message + "\n"
    print(log_message)
    with open(FILE_MAIN_LOG, 'a') as log_file:
        log_file.write(log_message)


def search_github(api_list, repo_name):
    rate_limit = g.get_rate_limit()
    if repo_name[0] == "/":
        repo_name = repo_name[1:]
    rate = rate_limit.search
    if rate.remaining == 0:
        print(f'\nYou have 0/{rate.limit} API calls remaining. Reset time: {rate.reset}')
        time.sleep(80)
    else:
        print(f'You have {rate.remaining}/{rate.limit} API calls remaining')

    keyword_list = ""
    count = 0
    for api_name in api_list:
        api_name = str(api_name).strip().replace(" ", "")
        query = f'"{api_name}" in:file repo:' + str(repo_name)
        log("Git Query: " + str(query))
        try:
            result = g.search_code(query, order='desc')
            log("Found " + str(result.totalCount) + " file(s)")
            count = int(result.totalCount)
        except Exception as e:
            print("Github Exception: " + str(e))
            count = 0
        if count > 0:
            break
    return count


def is_version_greater(base_version, comp_version):
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


def get_client_list(lib_name, target_version, version):
    global ONLY_COMPILATION_MODE, FILE_STATIC_CLIENT_LIST
    relevant_clients = []
    client_info_path = os.path.join(script_path, "..", "miner", "result", lib_name+"_version_info.txt")
    if not os.path.exists(client_info_path):
        print("the client info file does not exist!!!")
        exit(1)
    static_client_list = []
    if ONLY_COMPILATION_MODE:
        with open(FILE_STATIC_CLIENT_LIST) as list_file:
            content = list_file.readlines()
            for line in content:
                repo_name = line.split(",")[1].replace("https://www.github.com", "").strip().replace("\n", "")
                static_client_list.append(repo_name)
    with open(client_info_path) as f:
        repos = f.readlines()
        for repo in repos:
            client_repo, library_usage_version = repo.split()
            if ONLY_COMPILATION_MODE:
                if client_repo not in static_client_list:
                    continue
            if target_version == library_usage_version:
                relevant_clients.append(client_repo)
            else:
                is_client_version_higher = is_version_greater(target_version, library_usage_version)
                if version == "old" and not is_client_version_higher:
                    relevant_clients.append(client_repo)
                elif version == "new" and is_client_version_higher:
                    relevant_clients.append(client_repo)
    return relevant_clients


def get_repo(url, client_path):
    if not os.path.exists(client_path):
        try:
            url = url.replace("github", ":@github")
            git.Repo.clone_from(url, client_path)
        except:
            return 1

    return 0


def invoke_csharpengine(lib_name, old_version, new_version, client_name, old_client_version, new_client_version, client_path, target_version):
    binary_name = "CSharpEngine.exe"
    csharp_engine_path = os.path.join(script_path, "..", "CSharpEngine", "bin", "Debug", "net5.0", binary_name)
    if os.name == "posix" and not os.path.isfile(csharp_engine_path):
        binary_name = "CSharpEngine"
        csharp_engine_path = os.path.join(script_path, "..", "CSharpEngine", "bin", "Debug", "net5.0", binary_name)
    command = csharp_engine_path + " -l " + lib_name + " -m " + old_version + " -n " + new_version + " -c " \
             + client_name + " -s " + old_client_version + " -t  " + new_client_version + " -z " + target_version
    # print(command)
    log("[COMMAND]: " + str(command))
    return_code = subprocess.call(command, shell=True)
    log("[INFO] Ret Code: " + str(return_code))
    if return_code != 0:
        # command = "rm -rf " + client_path
        # subprocess.call(command, shell=True)
        log("failed to invoke CSharpEngine for Static Analysis")
        if str(return_code) == "1":
            log("invocation failed with return code: " + str(return_code))            
        else:
            log("invocation crashed with return code: " + str(return_code))           
        shutil.rmtree(client_path)
    return return_code


def invoke_csharpengine_compilation_mode(lib_name, old_lib_version, new_lib_version, client_name, old_client_version, new_client_version, client_path, target_version):
    binary_name = "CSharpEngine.exe"
    csharp_engine_path = os.path.join(script_path, "..", "CSharpEngine", "bin", "Debug", "net5.0", binary_name)
    if os.name == "posix" and not os.path.isfile(csharp_engine_path):
        binary_name = "CSharpEngine"
        csharp_engine_path = os.path.join(script_path, "..", "CSharpEngine", "bin", "Debug", "net5.0", binary_name)
       
    sln_file_list = pre_build(lib_name, client_name, old_client_version, target_version)
    if not sln_file_list:
        log("[Error] pre_build failed or no matching sln file is found")
        return 1
    return_code = 1
    for sln_file in sln_file_list:        
        command = csharp_engine_path + " -l " + lib_name + " -m " + old_lib_version + " -n " + new_lib_version + " -c " \
                 + client_name + " -s " + old_client_version + " -t  " + new_client_version + " -z " + target_version \
                 + " -y " + " -p " + sln_file
        log("[COMMAND]: " + str(command))
        return_code = subprocess.call(command, shell=True)
        log("[INFO] Ret Code: " + str(return_code))
        if return_code == 0:
            break
    if return_code != 0:
        if str(return_code) == "1":
            log("invocation failed with return code: " + str(return_code))
        else:
            log("invocation crashed with return code: " + str(return_code))
        shutil.rmtree(client_path)
    return return_code


def pre_build(lib_name, client_name, version, target_version):
    client_path = os.path.join(script_path, "..", "..", "benchmark", lib_name, "client", client_name + "-" + version)
    sln_file_list = []
    for root, dirs, files in os.walk(client_path):
        for file in files:
            if file.endswith('.sln'):
                sln_file_list.append(os.path.join(root, file))
    if len(sln_file_list) == 0:
        log("[Error] failed to find sln file for " + client_name)
        return None
    filtered_sln_file_list = []
    for sln_file in sln_file_list:
        sln_file_rel_path = sln_file.replace(client_path + "/", "")
        pre_build_command = "sln_path=`dirname " + sln_file + "`;"
        pre_build_command += "cd $sln_path;"
        pre_build_command += "dotnet.exe restore; " 
        pre_build_command += "nuget.exe restore;" 
        pre_build_command += "MSBuild.exe `basename " + sln_file_rel_path + "`"
        if os.name == "posix" and not os.path.isdir("/mnt/c"):
            pre_build_command = "sln_path=`dirname " + sln_file + "`;"
            pre_build_command += "cd $sln_path;"
            pre_build_command += "dotnet restore;" 
            pre_build_command += "nuget restore;" 
            pre_build_command += "dotnet msbuild `basename " + sln_file_rel_path + "`"

        log("[COMMAND]: " + str(pre_build_command))
        return_code = subprocess.call(pre_build_command, shell=True)
        log("[INFO] Ret Code: " + str(return_code))
        if return_code != 0:
            log("pre_build failed with return code: " + str(return_code))
        else:
            filtered_sln_file_list.append(sln_file_rel_path)
    return filtered_sln_file_list


def get_relevant_client(lib_name, repo_name, old_lib_version, new_lib_version, target_version):
    global ONLY_COMPILATION_MODE, FILE_STATIC_CLIENT_LIST, FILE_TYPED_CLIENT_LIST
    client_path = os.path.join(script_path, "..", "..", "benchmark", lib_name, "client")
    if not os.path.exists(client_path):
        os.mkdir(client_path)
    client_name = repo_name.split("/")[1] + "_" + repo_name.split("/")[2]
    client_path = os.path.join(client_path, client_name + "-1.0")
    client_url = "https://www.github.com" + repo_name
    status_clone = get_repo(client_url, client_path)
    log("[INFO] client cloned from " + "https://www.github.com" + repo_name + " to " + client_path)
    if status_clone != 0:
        return
    status_static = 0
    status_dynamic = 0
    if not ONLY_COMPILATION_MODE:
        status_static = invoke_csharpengine(lib_name, old_lib_version, new_lib_version, client_name, "1.0", "1.0", client_path, target_version)
        if status_static == 0:
            with open(FILE_STATIC_CLIENT_LIST, 'a') as log_static:
                log_static.write(client_name + "," + client_url + "\n")
            status_dynamic = invoke_csharpengine_compilation_mode(lib_name, old_lib_version, new_lib_version, client_name, "1.0", "1.0", client_path, target_version)
            if status_dynamic == 0:
                with open(FILE_TYPED_CLIENT_LIST, 'a') as log_static:
                    log_static.write(client_name + "," + client_url + "\n")
    else:
        status_dynamic = invoke_csharpengine_compilation_mode(lib_name, old_lib_version, new_lib_version, client_name, "1.0", "1.0", client_path, target_version)
        if status_dynamic == 0:
            with open(FILE_TYPED_CLIENT_LIST, 'a') as log_static:
                log_static.write(client_name + "," + client_url + "\n")
    if status_static == 0 and status_dynamic == 0:
        log("[INFO] C# Information Collected")
    else:
        log("[INFO] Unable to extract AST")


def fetch_client(client_list, target_version, api_list, library_name, old_version, new_version):
    length = len(client_list)
    print("the length of clients: ", len(client_list))
    if length > 1000:
        length = 1000
    with alive_bar(length) as bar:
        for i in range(length):
            client_name = client_list[i]
            # print(client)
            if not ONLY_COMPILATION_MODE:
                log("[INFO] Name: " + str(client_name))
                if client_name == "/thomasmichaelwallace/progDotNetNotes":
                    bar()
                    continue
                if search_github(api_list, client_name) == 0:
                    log("Skipping repo: " + str(client_name))
                    log("[INFO] Search Result is Empty")
                    bar()
                    continue
            get_relevant_client(library_name, client_name, old_version, new_version, target_version)
            bar()


def mine(lib_name=None):
    global FILE_MAIN_LOG, ONLY_NEW, ONLY_OLD, FILE_TYPED_CLIENT_LIST, FILE_STATIC_CLIENT_LIST
    breaking_changes = read_json(FILE_API_LIST)
    length = len(breaking_changes)

    for info in breaking_changes:
        library_name = info['library']
        if lib_name:
            if library_name != lib_name:
                continue
        version_old = info['source']
        version_new = info['target']
        log_name = library_name + "_" + str(version_old) + "_" + str(version_new)
        root_directory = "\\".join(os.path.realpath(__file__).split("\\")[:-3])
        if os.name == "posix":
            root_directory = "/".join(os.path.realpath(__file__).split("/")[:-3])
        FILE_MAIN_LOG = DIRECTORY_LOG + "/" + log_name
        lib_version_change = lib_name + "_" + str(version_old) + "_" + str(version_new)
        lib_change_dir = os.path.join(root_directory, "benchmark", lib_name, lib_version_change)
        FILE_STATIC_CLIENT_LIST = lib_change_dir + "/static-list"
        FILE_TYPED_CLIENT_LIST = lib_change_dir + "/typed-list"
        if not os.path.isfile(FILE_STATIC_CLIENT_LIST):
            with open(FILE_STATIC_CLIENT_LIST, 'w') as log_file:
                pass
        if not os.path.isfile(FILE_TYPED_CLIENT_LIST):
            with open(FILE_TYPED_CLIENT_LIST, 'w') as log_file:
                pass
        print("FILE_MAIN_LOG: " + FILE_MAIN_LOG)
        if os.path.isfile(FILE_MAIN_LOG):
            os.remove(FILE_MAIN_LOG)
        with open(FILE_MAIN_LOG, 'w+') as log_file:
            log_file.write("[Start] started at " + str(datetime.datetime.now()) + "\n")
        print(library_name, version_old, version_new)
        print("-"*100)
        old_client_list = list()
        new_client_list = list()
        if not ONLY_NEW:
            log("[INFO] reading old client list")
            old_client_list = get_client_list(library_name, version_old, "old")
            log("[INFO] read old clients: " + str(len(old_client_list)))
        if not ONLY_OLD:
            log("[INFO] reading new client list")
            new_client_list = get_client_list(library_name, version_new, "new")
            log("[INFO] read new clients: " + str(len(new_client_list)))
        if not ONLY_NEW:
            log("[INFO] fetching old clients")
            lib_api_list = info['old_apis']
            fetch_client(old_client_list, "old", lib_api_list, library_name, version_old, version_new)
        if not ONLY_OLD:
            log("[INFO] fetching new clients")
            lib_api_list = info['new_apis']
            fetch_client(new_client_list, "new", lib_api_list, library_name, version_old, version_new)


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description='Breaking-changes')
    parser.add_argument('-l', '--name', dest='name', help='the name of the library', required=False)
    parser.add_argument('-o', '--only-old', dest='old', type=bool,
                        help='only the old library versions', required=False)
    parser.add_argument('-n', '--only-new', dest='new', type=bool,
                        help='only the new library versions', required=False)
    parser.add_argument('-c', '--compilation-mode', dest='compilation', type=bool,
                        help='find usages only in compilation mode', required=False)
    # parser.add_argument('-t', '--target', dest='target', type=str,
    #                     help='the target version of library', required=True)
    args = parser.parse_args()
    mine_lib_name = args.name
    if not os.path.isdir(DIRECTORY_LOG):
        os.mkdir(DIRECTORY_LOG)
    if args.old:
        ONLY_OLD = True
    if args.new:
        ONLY_NEW = True
    ONLY_COMPILATION_MODE = args.compilation
    # target_version = args.target
    # check_version = args.old
    # if target_version == "new":
    #     check_version = args.new
    # relevant_client_edits = get_repo_info(library_name, check_version)
    # length = len(relevant_client_edits)
    mine(mine_lib_name)
