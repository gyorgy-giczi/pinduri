# Pinduri

I was just playing with programming in functional style, just for fun, trying to implement something well-known with minimal features in minimal size. Finally, I ended up with an Orm, a DI container, and a unit test engine, each less than 100 lines of code. It depends on how we count lines of code.

Some orm tests need Sql Server running:

    docker run -e "ACCEPT_EULA=Y" -e "SA_PASSWORD=Abcd123#" -p 1433:1433 -d mcr.microsoft.com/mssql/server:2019-latest

To run the tests:

    dotnet run
