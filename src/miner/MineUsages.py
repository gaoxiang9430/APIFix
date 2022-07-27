#!/usr/bin/env python

import argparse
import os
import shutil
import datetime
import subprocess
import sys
import git
import time
from github import Github
from alive_progress import alive_bar
import utils

DIRECTORY_ROOT = "/".join(os.path.realpath(__file__).split("/")[:-2])
DIRECTORY_LOG = DIRECTORY_ROOT + "/logs"
ACCESS_TOKEN = "66f7a4e70b3948b8665dc78b8a722e017c0a7e24"
FILE_STATIC_CLIENT_LIST = ""
FILE_TYPED_CLIENT_LIST = ""

ONLY_COMPILATION_MODE = False
ONLY_NEW = False
ONLY_OLD = False

script_path = os.path.dirname(os.path.realpath(__file__))
g = Github()
CONFIGURATION_FILE = None


def search_github(api_list, repo_name):
    rate_limit = g.get_rate_limit()
    if repo_name[0] == "/":
        repo_name = repo_name[1:]
    rate = rate_limit.search
    if rate.remaining == 0:
        utils.debug(f'\nYou have 0/{rate.limit} API calls remaining. Reset time: {rate.reset}')
        time.sleep(80)
    else:
        utils.debug(f'You have {rate.remaining}/{rate.limit} API calls remaining')

    keyword_list = ""
    count = 0
    for api_name in api_list:
        api_name = str(api_name).strip().replace(" ", "")
        query = f'"{api_name}" in:file repo:' + str(repo_name)
        utils.debug("Git Query: " + str(query))
        try:
            result = g.search_code(query, order='desc')
            utils.debug("Found " + str(result.totalCount) + " file(s)")
            count = int(result.totalCount)
        except Exception as e:
            utils.debug("Github Exception: " + str(e))
            count = 0
        if count > 0:
            break
    return count


def get_client_list(lib_name, target_version, version):
    global ONLY_COMPILATION_MODE, FILE_STATIC_CLIENT_LIST
    relevant_clients = []
    client_info_path = os.path.join(script_path, "..", "miner", "result", lib_name+"_version_info.txt")
    if not os.path.exists(client_info_path):
        utils.log("[ERROR] the client info file does not exist. Please run the crawler.py first!!! ")
        exit(1)
    static_client_list = []
    if ONLY_COMPILATION_MODE and os.path.exists(FILE_STATIC_CLIENT_LIST):
        with open(FILE_STATIC_CLIENT_LIST) as list_file:
            content = list_file.readlines()
            for line in content:
                repo_name = line.split(",")[1].replace("https://www.github.com", "").strip().replace("\n", "")
                static_client_list.append(repo_name)
    with open(client_info_path) as f:
        repos = f.readlines()
        for repo in repos:
            client_repo, library_usage_version = repo.split()
            if ONLY_COMPILATION_MODE and os.path.exists(FILE_STATIC_CLIENT_LIST):
                if client_repo not in static_client_list:
                    continue
            if target_version == library_usage_version:
                relevant_clients.append(client_repo)
            else:
                is_client_version_higher = utils.is_version_greater(target_version, library_usage_version)
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
    command = csharp_engine_path + \
              " -l " + lib_name + \
              " -m " + old_version + \
              " -n " + new_version + \
              " -c " + client_name + \
              " -s " + old_client_version + \
              " -t " + new_client_version + \
              " -z " + target_version + \
              " -f " + CONFIGURATION_FILE
    # utils.log(command)
    utils.debug("[COMMAND]: " + str(command))
    return_code = subprocess.call(command, shell=True)
    utils.debug("Ret Code: " + str(return_code))
    if return_code != 0:
        if str(return_code) == "1":
            utils.debug("invocation failed with return code: " + str(return_code))            
        else:
            utils.debug("invocation crashed with return code: " + str(return_code))           
        shutil.rmtree(client_path)
    return return_code


def invoke_csharpengine_compilation_mode(lib_name, old_lib_version, new_lib_version, client_name, old_client_version, new_client_version, client_path, target_version):
    binary_name = "CSharpEngine.exe"
    csharp_engine_path = os.path.join(script_path, "..", "CSharpEngine", "bin", "Debug", "net5.0", binary_name)
    if os.name == "posix" and not os.path.isfile(csharp_engine_path):
        binary_name = "CSharpEngine"
        csharp_engine_path = os.path.join(script_path, "..", "CSharpEngine", "bin", "Debug", "net5.0", binary_name)
       
    sln_file_list = utils.pre_build(lib_name, client_name, old_client_version)
    if not sln_file_list:
        utils.log("[Error] pre_build failed or no matching sln file is found")
        return 1
    return_code = 1
    for sln_file in sln_file_list:        
        command = csharp_engine_path + \
                  " -l " + lib_name + \
                  " -m " + old_lib_version + \
                  " -n " + new_lib_version + \
                  " -c " + client_name + \
                  " -s " + old_client_version + \
                  " -t  " + new_client_version + \
                  " -z " + target_version + \
                  " -y " + \
                  " -p " + sln_file + \
                  " -f " + CONFIGURATION_FILE
        utils.debug("[COMMAND]: " + str(command))
        return_code = subprocess.call(command, shell=True)
        utils.debug("Ret Code: " + str(return_code))
        if return_code == 0:
            break
    if return_code != 0:
        if str(return_code) == "1":
            utils.debug("invocation failed with return code: " + str(return_code))
        else:
            utils.debug("invocation crashed with return code: " + str(return_code))
        shutil.rmtree(client_path)
    return return_code


def get_relevant_client(lib_name, repo_name, old_lib_version, new_lib_version, target_version):
    global ONLY_COMPILATION_MODE, FILE_STATIC_CLIENT_LIST, FILE_TYPED_CLIENT_LIST
    client_path = os.path.join(script_path, "..", "..", "benchmark", lib_name, "client")
    if not os.path.exists(client_path):
        os.makedirs(client_path, exist_ok=True)
    client_name = repo_name.split("/")[1] + "_" + repo_name.split("/")[2]
    client_path = os.path.join(client_path, client_name + "-1.0")
    client_url = "https://www.github.com" + repo_name
    status_clone = get_repo(client_url, client_path)
    utils.debug("client cloned from " + "https://www.github.com" + repo_name + " to " + client_path)
    if status_clone != 0:
        return
    status_static = 0
    status_dynamic = 0
    if ONLY_COMPILATION_MODE:
        status_static = invoke_csharpengine(lib_name, old_lib_version, new_lib_version, client_name, "1.0", "1.0", client_path, target_version)
        if status_static == 0:
            with open(FILE_STATIC_CLIENT_LIST, 'a') as log_static:
                log_static.write(client_name + "," + client_url + "\n")
            status_dynamic = invoke_csharpengine_compilation_mode(lib_name, old_lib_version, new_lib_version, client_name, "1.0", "1.0", client_path, target_version)
            if status_dynamic == 0:
                with open(FILE_TYPED_CLIENT_LIST, 'a') as log_static:
                    log_static.write(client_name + "," + client_url + "\n")
    else:
        status_static = invoke_csharpengine(lib_name, old_lib_version, new_lib_version, client_name, "1.0", "1.0", client_path, target_version)
        if status_static == 0:
            with open(FILE_STATIC_CLIENT_LIST, 'a') as log_static:
                log_static.write(client_name + "," + client_url + "\n")
    if status_static == 0 and status_dynamic == 0:
        utils.debug("C# Information Collected")
    else:
        utils.debug("Unable to extract AST")


def fetch_client(client_list, target_version, api_list, library_name, old_version, new_version):
    length = len(client_list)
    utils.log("[INFO] the length of clients: " + str(len(client_list)))
    if length > 1000:
        length = 1000
    with alive_bar(length) as bar:
        for i in range(length):
            client_name = client_list[i]
            # utils.log(client)
            utils.log("[INFO] Mining client: " + str(client_name))
            if client_name == "/thomasmichaelwallace/progDotNetNotes":
                bar()
                continue
            if search_github(api_list, client_name) == 0:
                utils.log("[INFO] Skipping repo: " + str(client_name) + " since the Search Result is Empty")
                bar()
                continue
            get_relevant_client(library_name, client_name, old_version, new_version, target_version)
            bar()


def mine(breaking_change=None):
    global FILE_MAIN_LOG, ONLY_NEW, ONLY_OLD, FILE_TYPED_CLIENT_LIST, FILE_STATIC_CLIENT_LIST

    library_name = breaking_change['library']

    version_old = utils.trim_version_number(breaking_change['source'])
    version_new = utils.trim_version_number(breaking_change['target'])
    log_name = library_name + "_" + str(version_old) + "_" + str(version_new)
    root_directory = "\\".join(os.path.realpath(__file__).split("\\")[:-3])
    if os.name == "posix":
        root_directory = "/".join(os.path.realpath(__file__).split("/")[:-3])
    FILE_MAIN_LOG = DIRECTORY_LOG + "/" + log_name
    lib_version_change = library_name + "_" + str(version_old) + "_" + str(version_new)
    lib_change_dir = os.path.join(root_directory, "benchmark", library_name, lib_version_change)
    FILE_STATIC_CLIENT_LIST = lib_change_dir + "/static-list"
    FILE_TYPED_CLIENT_LIST = lib_change_dir + "/typed-list"
    if not os.path.isfile(FILE_STATIC_CLIENT_LIST):
        with open(FILE_STATIC_CLIENT_LIST, 'w') as log_file:
            pass
    if not os.path.isfile(FILE_TYPED_CLIENT_LIST):
        with open(FILE_TYPED_CLIENT_LIST, 'w') as log_file:
            pass
    utils.log("FILE_MAIN_LOG: " + FILE_MAIN_LOG)
    if os.path.isfile(FILE_MAIN_LOG):
        os.remove(FILE_MAIN_LOG)
    with open(FILE_MAIN_LOG, 'w+') as log_file:
        log_file.write("[Start] started at " + str(datetime.datetime.now()) + "\n")
    print(library_name, version_old, version_new)
    print("-"*100)
    old_client_list = list()
    new_client_list = list()
    if not ONLY_NEW:
        utils.debug("reading old client list")
        old_client_list = get_client_list(library_name, version_old, "old")
        utils.debug("read old clients: " + str(len(old_client_list)))
        utils.debug("fetching old clients")
        lib_api_list = breaking_change['old_apis']
        fetch_client(old_client_list, "old", lib_api_list, library_name, version_old, version_new)
    if not ONLY_OLD:
        utils.debug("reading new client list")
        new_client_list = get_client_list(library_name, version_new, "new")
        utils.debug("read new clients: " + str(len(new_client_list)))
        utils.debug("fetching new clients")
        lib_api_list = breaking_change['new_apis']
        fetch_client(new_client_list, "new", lib_api_list, library_name, version_old, version_new)

if __name__ == '__main__':
    parser = argparse.ArgumentParser(description='Mine usages of APIs')
    # parser.add_argument('-l', '--name', dest='name', help='the name of the library', required=True)
    parser.add_argument('-f', '--config_file', dest='config_file', help='the path to the configuration file', required=True)

    parser.add_argument('--only-old', dest='old', action="store_true",  
                        help='only the old library versions', required=False)
    parser.add_argument('--only-new', dest='new', action="store_true",
                        help='only the new library versions', required=False)
    parser.add_argument('--compilation-mode', dest='compilation', action="store_true",
                        help='mine usages in compilation mode', required=False)
    # parser.add_argument('-t', '--target', dest='target', type=str,
    #                     help='the target version of library', required=True)
    args = parser.parse_args()

    if not os.path.isdir(DIRECTORY_LOG):
        os.mkdir(DIRECTORY_LOG)

    if args.old:
        ONLY_OLD = True
    if args.new:
        ONLY_NEW = True

    ONLY_COMPILATION_MODE = args.compilation

    CONFIGURATION_FILE = args.config_file
    if not os.path.isfile(CONFIGURATION_FILE):
        utils.log("[ERROR] The configuration file does not exist!!!")
        exit(1)
    breaking_change = utils.read_json(CONFIGURATION_FILE)

    mine(breaking_change)
