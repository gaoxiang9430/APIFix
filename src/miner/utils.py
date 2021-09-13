import subprocess
import time
import os
import json


sleepTime = 10
user = None
token = None

script_path = os.path.dirname(os.path.realpath(__file__))
FILE_MAIN_LOG = "temp.log"


def log(log_message):
    global FILE_MAIN_LOG
    log_message = "[" + str(time.asctime()) + "] " + log_message + "\n"
    print(log_message)
    with open(FILE_MAIN_LOG, 'a') as log_file:
        log_file.write(log_message)


def read_url(address):
    if user is None or token is None:
        return read_url2(address)
    try:
        html = subprocess.check_output(
            ['curl', '-s', '-u', user + ":" + token, address])
    except:
        html = ""
    time.sleep(0.1)
    return html


# read the content of target url
def read_url2(address):
    html = subprocess.check_output(
        ['curl', '-s', address])
    time.sleep(sleepTime)
    return html


def read_json(file_path):
    with open(file_path, 'r', encoding='utf-8', errors='ignore') as in_file:
        json_data = json.load(in_file)
        return json_data


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


def pre_build(lib_name, repo_name, version):
    if lib_name != repo_name:
        repo_path = os.path.join(script_path, "..", "..", "benchmark", lib_name, "client", repo_name + "-" + version)
    else:
        repo_path = os.path.join(script_path, "..", "..", "benchmark", lib_name, "library", repo_name + "-" + version)
    sln_file_list = []
    for root, dirs, files in os.walk(repo_path):
        for file in files:
            if file.endswith('.sln'):
                sln_file_list.append(os.path.join(root, file))
    if len(sln_file_list) == 0:
        log("[Error] failed to find sln file for " + repo_name)
        return None
    filtered_sln_file_list = []
    for sln_file in sln_file_list:
        sln_file_rel_path = sln_file.replace(repo_path + "/", "")
        if os.name == "posix" and not os.path.isdir("/mnt/c"):
            pre_build_command = "sln_path=`dirname " + sln_file + "`;"
            pre_build_command += "cd $sln_path;"
            pre_build_command += "dotnet restore > /dev/null; ;" 
            pre_build_command += "nuget restore > /dev/null; ;" 
            pre_build_command += "dotnet msbuild `basename " + sln_file_rel_path + "` > /dev/null"
        else:
            pre_build_command = "sln_path=`dirname " + sln_file + "`;"
            pre_build_command += "cd $sln_path;"
            pre_build_command += "dotnet.exe restore > /dev/null; " 
            pre_build_command += "nuget.exe restore > /dev/null; "
            pre_build_command += "MSBuild.exe `basename " + sln_file_rel_path + "` > /dev/null"

        log("[COMMAND]: " + str(pre_build_command))
        return_code = subprocess.call(pre_build_command, shell=True)
        log("[INFO] Ret Code: " + str(return_code))
        if return_code != 0:
            log("pre_build failed with return code: " + str(return_code))
        else:
            filtered_sln_file_list.append(sln_file_rel_path)
    return filtered_sln_file_list

