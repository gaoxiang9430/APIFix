#!/usr/bin/env python

# This is program is used to crawler subjects from github
# Author: Xiang Gao Email: gaoxiang@comp.nus.edu.sg

import sys
import json
import re
import utils
import argparse
import os.path
import extractdata
import subprocess
import datetime
import io


class Miner:

    def __init__(self, library = None, package = None, author = None, max_dependent = 500):
        self.library_name = library
        self.package_name = package
        self.author_name = author
        library_name_id = library
        self.max_deps = max_dependent/20
        if not os.path.exists("result"):
            os.mkdir("result")
        self.dep_result = "result/" + library_name_id + "_dep_result.txt"
        self.mine_result = "result/" + library_name_id + "_result.txt"
        self.mine_result_json = "result/" + library_name_id + "_result.json"
        self.saved_dependents = []
        self.depth = 1

    def get_all_dependent(self):
        dependents = "/network/dependents"
        #if self.id != None:
        #    dependents = dependents + "?package_id=" + self.id
        print("searching clint programs from: https://github.com/" + self.author_name + "/" + self.library_name + dependents)
        return self.get_all_dependent_sub("https://github.com/" +
                                          self.author_name + "/" + self.library_name + dependents)

    def get_all_dependent_sub(self, dependent_addr):
        result = []
        librarys = open(self.dep_result, 'a')
        librarys.seek(io.SEEK_END)
        content = utils.read_url(dependent_addr)
        if content == "":
        	return result
        lines = content.decode().split('\n')
        for line in lines:
            links = re.match(r'.*ata-hovercard-type="repository" data-hovercard-url=\".*\" href=\"(.*)\".*', line, re.S)
            if links:  # find first match
                client = links.group(1)

                '''
                repo_info_command = "https://api.github.com/repos" + client
                commits = utils.read_url(repo_info_command)
                repo_info = json.loads(commits.decode('utf-8'))
                if "pushed_at" not in repo_info:
                    continue
                time_stamp = datetime.datetime.strptime(repo_info["pushed_at"], "%Y-%m-%dT%H:%M:%SZ").timetuple()
                if time_stamp.tm_year < 2019 or (time_stamp.tm_year == 2019 and time_stamp.tm_mon < 7):
                    continue
                '''
                # if repo_info["stargazers_count"] < 1:
                #     continue
                # print(client)
                result.append(client)
                librarys.write(str(links.group(1)) + "\n")

        lines.reverse()
        for line in lines:
            next = re.match(r'.*class=\"btn btn-outline BtnGroup-item\" href=\"(.*)\">Next', line, re.S)
            if next and self.depth < self.max_deps:  # find next
                self.depth += 1
                if "Previous" in next.group(1):
                    break
                print(next.group(1))
                result += self.get_all_dependent_sub(next.group(1))
                break

        librarys.close()
        return result

    def extract_version(self, content, lib_name):
        lines = content.split('\n')
        for line in lines:
            links = re.match(r'.*(?<!\.).*' + lib_name + '(?!\.).*ersion.*', line, re.S)
            if links:  # find first match
                version = extractdata.extract_version(line)
                return version

        return None

    def get_file_include_library(self, dependent):
        files_indicate_lib = []
        package_name = self.package_name
        if package_name == "WindowsAzure.Storage":
            package_name = "WindowsAzure"
        search_csproj = "https://api.github.com/search/code?q=" + \
                        package_name + "+in:file+repo:" + dependent[1:]

        file_content = utils.read_url(search_csproj)
        if file_content == "":
        	return files_indicate_lib
        json_file = json.loads(file_content.decode('utf-8'))
        if "items" in json_file:
            files = json_file["items"]
            for i in range(len(files)):
                file_path = files[i]["path"]
                if " " in file_path:  # does not support the file path with space
                    continue
                if "csproj" in file_path or "nuspec" in file_path or "config" in file_path or "props" in file_path:
                    file_content = utils.read_url("https://github.com" + dependent + "/blob/master/" + file_path)
                    # if "csproj" in file_path or "nuspec" in file_path:
                    #     file_content = utils.read_url(files[i]["html_url"])
                    if file_content == None or file_content == "":
                        continue
                    if self.package_name in file_content.decode():
                        version = self.extract_version(file_content.decode(), self.package_name)
                        if version != None:
                            files_indicate_lib.append((file_path, version))
        # print(files_indicate_lib)
        return files_indicate_lib

    def get_five_day_commit(self, dep, commit):
        date = commit["commit"]["committer"]["date"]
        time_stamp = datetime.datetime.strptime(date, "%Y-%m-%dT%H:%M:%SZ") + datetime.timedelta(days=5)
        commit_api = "https://api.github.com/repos" + dep + "/commits?" + \
                     "since=" + date + \
                     ";until=" + time_stamp.strftime("%Y-%m-%dT%H:%M:%SZ")
        commits = utils.read_url(commit_api)
        if commits == "":
        	return None
        commits_json = json.loads(commits.decode('utf-8'))
        if len(commits_json) == 0:
            return None
        return commits_json[0]["sha"]

    def visit_dependent(self, dependent):
        visited_commits = []
        edit_items = []
        files_indicate_lib = self.get_file_include_library(dependent)
        # result_out = open(self.mine_result, "a")
        for file_indicate_lib in files_indicate_lib:
            self.log_version_info(dependent, file_indicate_lib[1])

            commit_api = "https://api.github.com/repos" + dependent + "/commits?path=" + file_indicate_lib[0]
            commits = utils.read_url(commit_api)
            if commits == "" or commits == None:
                continue
            try:
            	commit_json = json.loads(commits.decode('utf-8'))
            except:
            	continue
            for commit in commit_json:
                if commit["html_url"] in visited_commits:
                    continue
                else:
                    visited_commits.append(commit["html_url"])

                commit_content = utils.read_url(commit["html_url"])
                if commit_content == "":
                    continue
                delete, insert = self.include_library_update(commit_content.decode())
                if delete is not None and insert is not None:
                    # print(commit["html_url"] + "\n -------- Delete:" + delete + "\n -------- Insert:" + insert + "\n")
                    # result_out.write(commit["html_url"] + "\n ------ Delete:" + delete + "\n ------ Insert:" + insert)
                    html_url = "https://github.com" + dependent + ".git"
                    old_version = extractdata.extract_version(delete)
                    new_version = extractdata.extract_version(insert)
                    if len(commit["parents"]) > 0 and old_version is not None and new_version is not None:
                        old_commit_id = commit["parents"][0]["sha"]
                        new_commit_id = commit["sha"]
                        client_name = dependent.split("/")[-1]
                        five_day_commit = self.get_five_day_commit(dependent, commit)
                        if five_day_commit is None:
                            five_day_commit = new_commit_id
                        edit_items.append(extractdata.UpdateItem(html_url, old_version, new_version,
                                                                 old_commit_id, new_commit_id, client_name,
                                                                 five_day_commit))
        # result_out.close()
        return edit_items

    def read_dependent_from_file(self):
        if not os.path.exists(self.dep_result):
            return self.get_all_dependent()
        result = []
        librarys = open(self.dep_result, 'r')
        lines = librarys.readlines()
        librarys.close()

        for line in lines:
            if "https://github.com" not in line:
                result.append(line[:-1])
        return result

    def include_library_update(self, content):
        find_delete = None
        find_insert = None

        lines = content.split('\n')
        for line in lines:
            links = re.match(r'.*data-code-marker=\"-\">(.*)(?<!\.)' + self.package_name + '(?!\.).*ersion.*', line, re.S)
            if links:  # find first match
                find_delete = line

            links = re.match(r'.*data-code-marker=\"\+\">(.*)(?<!\.)' + self.package_name + '(?!\.).*ersion.*', line, re.S)
            if links:  # find first match
                find_insert = line

            if find_delete and find_insert:
                break

        return find_delete, find_insert

    def log_mine_result(self, dependents):
        update_items = []

        old_len = 0
        length = len(dependents)
        print("mining the library versions used by each clients:")
        for i in range(length):
            dependent = dependents[i]
            update_items += miner.visit_dependent(dependent)
            if len(update_items) > old_len:
                old_len = len(update_items)

                result_out = open(self.mine_result, "w")
                data = json.dumps(update_items, default=extractdata.dumper)
                result_out.write(data.replace("\\n", "").replace("\\", "").replace("\"{", "{").replace("}\"", "}"))
                result_out.close()

            progress = int(float(100*i) / (length-1) / 2)
            sys.stdout.write('\r')
            sys.stdout.write("[%-50s] %d%%" % ('='*progress, 2*progress))
            sys.stdout.flush()
        print()
		
        command = "python3 -m json.tool " + self.mine_result + " > " + self.mine_result_json
        print("the mining results are saved to " + self.mine_result_json)
        subprocess.call(command, shell=True)


    def log_current_library_version(self, dependents):
        length = len(dependents)
        for i in range(length):
            dependent = dependents[i]
            files_indicate_lib = self.get_file_include_library(dependent)
            for file_indicate_lib in files_indicate_lib:
                log_version_info(dependent, file_indicate_lib[1])
                break

    def log_version_info(self, dependent, version):
        if dependent in self.saved_dependents:
            return
        self.saved_dependents.append(dependent)
        version_out = open("result/" + self.library_name + "_version_info.txt", "a")
        version_out.write(dependent + " " + version + "\n")
        version_out.close()

if __name__ == '__main__':
    parser = argparse.ArgumentParser(description='Mine clients of a github library.')
    parser.add_argument('repo', type=str, help='the name of repository')
    parser.add_argument('author', type=str, help='the name of repository author')
    parser.add_argument('package', type=str, help='the name of package')
    parser.add_argument('-m', '--max', type=int, default=500, help='the max number of clients to explore')
    parser.add_argument('-u', '--user', type=str, default=None, help='the git user name for mining')
    parser.add_argument('-t', '--token', type=str, default=None, help='token for mining')
    parser.add_argument('--only-latest', dest="only_latest", action="store_true", 
                        help='only mine the latest library version used by the clients')

    args = parser.parse_args()
    if len(args.repo) <= 0 or len(args.author) <= 0:
        print(parser)
        exit(1)

    author_name = args.author
    repo_name = args.repo
    package_name = args.package
    if package_name == None:
    	package_name = repo_name

    if args.user is not None and args.token is not None:
        utils.user = args.user
        utils.token = args.token

    miner = Miner(repo_name, package_name, author_name, args.max)
    
    dependents = miner.read_dependent_from_file()
    #if len(dependents) > 1500:
    #    dependents = dependents[:1500]
    # miner.get_file_include_library("/cbarreto07/LustProject")
    if args.only_latest:
        log_current_library_version(dependents)
    else:
        miner.log_mine_result(dependents)

