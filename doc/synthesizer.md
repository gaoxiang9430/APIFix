
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