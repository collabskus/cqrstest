This is great progress. 
It builds great. 
When I switch to warning in appsettings, the output is gorgeous. 
A couple of issues: 
I think we need to add something like shell: bash somewhere in the github actions yaml 
Run dotnet publish MultiDbSync/MultiDbSync.Console/MultiDbSync.Console.csproj \
ParserError: D:\a\_temp\8dd59306-42a8-4d49-87fc-1da11182ecde.ps1:3
Line |
   3 |    --configuration Release \
     |      ~
     | Missing expression after unary operator '--'.
Error: Process completed with exit code 1.
Also we use mediatR 12.2.0 for which there is no update path. 
lets get rid of it and do this ourselves without any dependency. 
add polly if you need to 
but please get rid of mediatR
please give full files for all the files that need to change for this 
please and thank you 
