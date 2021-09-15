# Mining relevant human adaptations and API usages for breaking changes

## Mine relevant clients of a github library

Github Dependency graph
https://docs.github.com/en/code-security/supply-chain-security/understanding-your-software-supply-chain/about-the-dependency-graph

```
usage: crawler.py [-h] [-u USER] [-t TOKEN] repo author package

Mine the clients of a github library

positional arguments:
  repo                  	  the name of library
  author                	  the author name of library
  package               	  the package name

optional arguments:
  --only_latest             only mine the latest library version used by the clients
                            (mining the history of clients can be very slow)
  -u USER, --user USER  	  the git user name for mining
  -t TOKEN, --token TOKEN   token for mining
  
  -h, --help            	  show this help message and exit
```

## Prepare a configuration file

```
{
    "library": "LIBRARY_NAME",
    "source": "OLD_VERSION_NUMBER",
    "target": "NEW_VERSION_NUMBER",
    "old_apis": [
        "OLD_API_NAME"
    ],
    "new_apis": [
        "NEW_API_NAME"
    ]
}
```

## Mine humam adaptations for breaking changes

```
usage: MineEdit.py [-h] -f CONFIG_FILE [--only-mine-library]
                   [--compilation-mode]
                   author library

Mine humam adaptations for breaking changes

positional arguments:
  author                the author name of the library
  library               the name of the library

optional arguments:
  -h, --help            show this help message and exit
  -f CONFIG_FILE, --config_file CONFIG_FILE
                        the path to the configuration file
  --only-mine-library   only mine the human adaptations from library itself
  --compilation-mode    mine human edit in compilation mode
```

## Mine usages of APIs

```
usage: MineUsages.py [-h] -l NAME -f CONFIG_FILE [--only-old] [--only-new]
                     [--compilation-mode]

Mine usages of APIs

optional arguments:
  -h, --help            show this help message and exit
  -l NAME, --name NAME  the name of the library
  -f CONFIG_FILE, --config_file CONFIG_FILE
                        the path to the configuration file
  --only-old            only the old library versions
  --only-new            only the new library versions
  --compilation-mode    find usages only in compilation mode
```
