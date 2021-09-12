import subprocess
import time

sleepTime = 10
user = None
token = None


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


