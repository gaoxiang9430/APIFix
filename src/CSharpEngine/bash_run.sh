set -x

CompilatioMode="-y"

#./bin/Debug/net5.0/CSharpEngine.exe -l Polly -m 5.5.0 -n 6.1.2 -i -p src/Polly.sln
#./bin/Debug/net5.0/CSharpEngine.exe -l Polly -m 6.1.2 -n 7.0.0 -i -p src/Polly.sln

#./bin/Debug/net5.0/CSharpEngine.exe -l FluentValidatation -m 8.0.0 -n 9.0.0 -i -p src/

#./bin/Debug/net5.0/CSharpEngine.exe -l MediatR -m 6.0.0 -n 7.0.0 -i -p MediatR.sln

#./bin/Debug/net5.0/CSharpEngine.exe -l SteamKit -m 1.8 -n 2.0 -i -p SteamKit2/SteamKit2.sln
#./bin/Debug/net5.0/CSharpEngine.exe -l SteamKit -m 2.0 -n 2.1 -i -p SteamKit2/SteamKit2.sln
#./bin/Debug/net5.0/CSharpEngine.exe -l SteamKit -m 2.1 -n 2.2 -i -p SteamKit2/SteamKit2.sln
#./bin/Debug/net5.0/CSharpEngine.exe -l SteamKit -m 2.2 -n 2.3 -i -p SteamKit2/SteamKit2.sln

#./bin/Debug/net5.0/CSharpEngine -l NAudio -m 1.8.0 -n 1.8.4 -i -p NAudio.sln
#./bin/Debug/net5.0/CSharpEngine -l NAudio -m 1.8.3 -n 1.10 -i -p NAudio.sln
#./bin/Debug/net5.0/CSharpEngine -l NAudio -m 1.10 -n 2.0 -i -p NAudio.sln


#./bin/Debug/net5.0/CSharpEngine -l AutoFixture -m 4.0 -n 4.4 -i -p Src/AutoFixture.sln

j=2.80.2; for i in 2.80.0 1.68.3 1.68.1.1 1.68.0 1.59.2; do ./bin/Debug/net5.0/CSharpEngine -l SkiaSharp -m $i -n $j -i -p binding/SkiaSharp.sln; j=$i; done

./bin/Debug/net5.0/CSharpEngine -l cscore -m 1.1.0 -n 1.2.1 -i -p CSCore.sln

./bin/Debug/net5.0/CSharpEngine -l ZXing.Net -m 0.16.2.0 -n 0.16.4.0 -i -p zxing.sln
./bin/Debug/net5.0/CSharpEngine -l ZXing.Net -m 0.16.4.0 -n 0.16.5.0 -i -p zxing.sln
./bin/Debug/net5.0/CSharpEngine -l ZXing.Net -m 0.16.5.0 -n 0.16.6.0 -i -p zxing.sln

j=2.10.0 for i in 2.9.1 2.5.0 2.4.0 2.2.0 2.1.0 2.0.0 1.22.0; do ./bin/Debug/net5.0/CSharpEngine -l MimeKit -m $i -n $j -i -p MimeKit.sln; j=$i; done
