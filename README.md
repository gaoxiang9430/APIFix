# Output-Oriented Program Synthesis for Combating Breaking Changes in Libraries #

APIFix is a tool that automates API usage adaptations by learning from the existing human actions via an output-oriented program synthesis. The aim is not only to rely on the example human adaptations of the clients from the old library version
to the new library version, since this can lead to over-fitting transformation rules. Instead, APIFix also relies on
example usages of the new updated library in clients, which provide valuable context for synthesizing and
applying the transformation rules.

### How does it work?


### Build


### Usage

```
usage: crawler.py [-h] [-u USER] [-t TOKEN] repo author package

positional arguments:
  repo                  	the name of repository
  author                	the name of repository author
  package               	the package name

optional arguments:
  --only_latest             only mine the latest library version used by the clients
  -u USER, --user USER  	the git user name for mining
  -t TOKEN, --token TOKEN   token for mining
  
  -h, --help            	show this help message and exit
```

```
usage: MineEdit.py [-h] -o OLD -n NEW name

positional arguments:
  name               the name of the library

optional arguments:
  -h, --help         show this help message and exit
  -o OLD, --old OLD  the old library version
  -n NEW, --new NEW  the new library versions
```

```
usage: MineUsages.py [-h] [-l NAME] [-o OLD] [-n NEW] [-c COMPILATION]

optional arguments:
  -h, --help            show this help message and exit
  -l NAME, --name NAME  the name of the library
  -o OLD, --only-old OLD
                        only the old library versions
  -n NEW, --only-new NEW
                        only the new library versions
  -c COMPILATION, --compilation-mode COMPILATION
                        find usages only in compilation mode
```

```
usage: CSharpEngine.exe -l LIBRARY -m OLD_VERSION -n NEW_VERSION [OPTIONS]

options:
  -l, --libraryName    Required. The name of library.
  -m, --oldLib         Required. The old version of library.
  -n, --newLib         Required. The new version of library.

  -i                   Extract relevent edit from library update itself.

  -c, --clientName     The name of the client.
  -s, --oldClient      The old version of client.
  -t, --newClient      The new version of client.

  -z                   Extract old/new usages.

  -y                   Compliation mode
  -p, --sln            The path to the sln file.

  -v, --verbose        Set output to verbose messages.
  --help               Display this help screen.
  --version            Display version information.
```

```
Synthesizer.exe -l LIBRARY -m OLD_VERSION -n NEW_VERSION [OPTIONS]

options:
  -l, --libraryName         Required. The name of library.
  -m, --oldLib              Required. The old version of library.
  -n, --newLib              Required. The new version of library.

  -s, --TargetAPI           Required. The name of old target API for fixing.
  -t, --TargetAPI           The name of new target API for fixing.

  --t1                      (Default: 0.15) The threshold for old usages [0, 1].
  --t2                      (Default: 0.25) The threshold for new usages [0, 1].

  -o, --additionalOutput    (Default: false) Use additionalOutput when synthesizing adaptation rule.
  -i, --additionalInput     (Default: false) Use additionalInput when synthesizing adaptation rule.

  -v, --verbose             Set output to Global.verbose messages.
  --help                    Display this help screen.
  --version                 Display version information.
```