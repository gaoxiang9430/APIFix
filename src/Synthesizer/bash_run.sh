#!/bin/bash
set -x

dotnet build

./bin/Debug/net5.0/Synthesizer.exe -m 5.5.0 -n 6.1.2 -l Polly -s Execute --t1=0.05 -v > Polly_Execute.txt
./bin/Debug/net5.0/Synthesizer.exe -m 6.1.2 -n 7.0.0 -l Polly -s WrapAsync -v > Polly_WrapAsync.txt
./bin/Debug/net5.0/Synthesizer.exe -m 6.1.2 -n 7.0.0 -l Polly -s GetAsync -t TryGetAsync -v > Polly_GetAsync.txt

./bin/Debug/net5.0/Synthesizer.exe -m 8.0.0 -n 9.0.0 -l FluentValidation -s Validate --t1=0.3 -v > FluentValidation_Validate.txt

./bin/Debug/net5.0/Synthesizer.exe -m 5.0.1 -n 6.0.0 -l MediatR -s Handle --t1=0.2 -v > MediatR_Handle.txt
./bin/Debug/net5.0/Synthesizer.exe -m 6.0.0 -n 7.0.0 -l MediatR -s Process --t1=0.2 -v > MediatR_Process.txt

./bin/Debug/net5.0/Synthesizer.exe -m 0.8 -n 0.9 -l Blazorise -s SetParametersAsync -v > Blazorise_SetParametersAsync.txt

./bin/Debug/net5.0/Synthesizer.exe -m 3.3.5 -n 4.0.0 -l DbUp -s AdHocSqlRunner -v > DbUp_AdHocSqlRunner.txt
./bin/Debug/net5.0/Synthesizer.exe -m 3.3.5 -n 4.0.0 -l DbUp -s SqlScriptExecutor -v > DbUp_SqlScriptExecutor.txt
./bin/Debug/net5.0/Synthesizer.exe -m 3.3.5 -n 4.0.0 -l DbUp -s StoreExecutedScript -v > DbUp_StoreExecutedScript.txt

./bin/Debug/net5.0/Synthesizer.exe -m 2.0 -n 2.1 -l SteamKit -s Disconnect -v > SteamKit_Disconnect.txt

./bin/Debug/net5.0/Synthesizer.exe -m 7.0.0 -n 8.0.0 -l AutoMapper -s Ignore -t DoNotValidate -v > AutoMapper_Ignore.txt
