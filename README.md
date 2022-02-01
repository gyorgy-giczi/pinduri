# Pinduri

I was just playing with programming in functional style using Linq, just for fun.

My goal is to learn from implementing some minimalistic software components that still work. Minimal feature set, minimal code size, minimal dependecies. Runtime performance is not important at all. Depending on how we count lines of code, each are less than or around 100 lines of code.

- **DI container**
- **Orm**: schema generation with indexes and foreign keys, CRUD logic
- **Json serializer**
- **Unit test engine**: time measurement, logging to console in colors, async tests, before/after hooks
- **Source code manager**: inspired by Git, single-user, not distributed, support for branching/merging/diff/history
- **Diff-merge algorithm**
- **Wildcard pattern matcher**

All stuff is put into one single repository, however they are independent to each other.

For formatting, the rule is simple: one expression can be either split into multiple lines for readability, or can be kept in one single line.

## Running tests

    dotnet run

Some orm tests need Sql Server:

    docker run -e "ACCEPT_EULA=Y" -e "SA_PASSWORD=Abcd123#" -p 1433:1433 -d mcr.microsoft.com/mssql/server:2019-latest

## Playing with the source code manager

It can be helpful to create alias for the scm cli.

    # sh:
    alias pinduri-scm="dotnet <path to pinduri.dll> scm"

    # powershell:
    function pinduri-scm { dotnet <path to pinduri.dll> scm $args }

    mkdir roarr
    cd roarr
    echo 'roarr' > roarr.txt
    pinduri-scm status
    pinduri-scm stage roarr.txt
    pinduri-scm status
    pinduri-scm commit "add roarr.txt"
    pinduri-scm status
    pinduri-scm history
    pinduri-scm branch my-branch
    echo 'ROARR' > roarr.txt
    pinduri-scm stage roarr.txt
    pinduri-scm commit "change roarr"
    pinduri-scm checkout default
    echo 'roarr!' > roarr.txt
    pinduri-scm stage roarr.txt
    pinduri-scm commit "fix roarr"
    pinduri-scm merge my-branch
    cat roarr.txt
    echo 'ROARR!' > roarr.txt
    pinduri-scm stage roarr.txt
    pinduri-scm commit "merge branch my-branch"
    pinduri-scm history
