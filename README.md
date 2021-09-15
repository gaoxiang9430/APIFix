# Output-Oriented Program Synthesis for Combating Breaking Changes in Libraries #

**Half-baked project. The interface is intensively updated.**

APIFix is a tool that automates API usage adaptations by learning from the existing human actions via an output-oriented program synthesis. The aim is not only to rely on the example human adaptations of the clients from the old library version
to the new library version, since this can lead to over-fitting transformation rules. Instead, APIFix also relies on
example usages of the new updated library in clients, which provide valuable context for synthesizing and
applying the transformation rules.

## Build ##

To install APIFix, you can either use our docker image or build it from source on Windows or Linux system. On Windows, you need to install Visual Studio with dotnet-sdk-5.0 and MSBUILD support. On linux, you need to install dotnet-runtime-5.0 and dotnet-sdk-5.0. Furthermore, it requires python3 library gitpython, alive-progress and PyGithub. To compile APIFix, simply run the following command:

```
cd src
dotnet restore
dotnet build
```
To check your installation, you can run the test cases.

```
dotnet test
```

## Usage ##
To use APIFix, there are two steps: (1) mining existing human adaptations for fixing breaking changes and the API usages (2) invoke synthesizer to automate API adaptations. The instructions on how to use our miner can be found in [doc/miner.md](doc/miner.md) and how to use the synthesizer is described in [doc/synthesizer.md](doc/synthesizer.md)


## Publication ##
**APIFix: Output-Oriented Program Synthesis for Combating Breaking Changes in Libraries** [[pdf]](https://www.comp.nus.edu.sg/~gaoxiang/papers/APIFix.pdf)<br>
Xiang Gao, Arjun Radhakrishna, Gustavo Soares, Ridwan Shariffdeen, Sumit Gulwani, Abhik Roychoudhury <br>
*Object-Oriented Programming, Systems, Languages, and Applications (OOPSLA) 2021*

