# AspNetCoreDevJwts

## What is this?
I logged [this issue in ASP.NET Core](https://github.com/dotnet/aspnetcore/issues/39857) for .NET 7 to explore ways we can make working with JWT authentication during development easier. Going from "I have no protected APIs" to "I added my first protected API" involves a lot of new concepts and actions today in ASP.NET Core. The primary focus of this exploration is to make that journey much easier to achieve.

## Video
### First Look at ASP NET Core dev JWTs
[![First Look at ASP NET Core dev JWTs](http://img.youtube.com/vi/xyp3urUm69I/0.jpg)](https://www.youtube.com/watch?v=xyp3urUm69I&feature=youtu.be&hd=1 "First Look at ASP NET Core dev JWTs")

## Install pre-requisites
1. Install the latest [.NET 7 Preview SDK](https://dotnet.microsoft.com/en-us/download/dotnet/7.0)

    On Windows you can optionally use [winget](https://docs.microsoft.com/en-us/windows/package-manager/winget/):
    ```
    winget install --id=Microsoft.dotnetPreview  -e
    ```
1. If using Visual Studio, install the latest [Visual Studio 2022 Preview](https://visualstudio.microsoft.com/vs/preview#download-preview)
    
    On Windows you can optionally use [winget](https://docs.microsoft.com/en-us/windows/package-manager/winget/):
    ```
    winget install --id=Microsoft.VisualStudio.2022.Community-Preview  -e
    ```

    **NOTE:** The ASP.NET and web development workload **must be installed even if you have a stable version of Visual Studio with the listed components**:

    <img width="319" alt="image" src="https://user-images.githubusercontent.com/45293863/164279629-1955ec11-0780-4705-88b4-d541874a4a02.png">

## Getting starting
1. After cloning this repo, build the solution in VS or at the command line:

    ```shell
    AspNetCoreDevJwts> dotnet build
    ```
1. Change to the `SampleWebApi` project directory:

    ```shell
    AspNetCoreDevJwts> cd SampleWebApi
    AspNetCoreDevJwts\SampleWebApi> 
    ```
1. Run the `dev-jwts` exe to print the CLI help:

    ```shell
    AspNetCoreDevJwts\SampleWebApi> .\DevJwts.Cli\bin\Debug\net7.0\dev-jwts.exe
    USAGE:
    dev-jwts [OPTIONS] <COMMAND>

    EXAMPLES:
        dev-jwts create
        dev-jwts create -n testuser --claim scope=myapi:read
        dev-jwts list
        dev-jwts delete caa676ee
        dev-jwts clear

    OPTIONS:
        -h, --help       Prints help information
        -v, --version    Prints version information

    COMMANDS:
        list      Lists all JWTs for the specified project
        create    Creates a JWT for the specified project
        print     Prints the details of the specified JWT
        delete    Deletes the JWT with the specified ID in the specified project
        clear     Deletes all JWTs for the specified project
        key       Prints the key used for signing JWTs for the specified project
    ```
1. Execute `dev-jwts.exe create` to create your first dev JWT for the project:

    ```shell
    AspNetCoreDevJwts\SampleWebApi> ..\DevJwts.Cli\bin\Debug\net7.0\dev-jwts.exe create
    JWT successfully created:
    Id:       c8a6e1d6
    Name:     damia
    Audience: https://localhost:7157
    Claims:   [none]
    Expires:  2022-10-22T17:50:25.2075766+00:00
    Issued:   2022-04-22T17:50:25.2443510+00:00
    Compact Token:    eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJ1bmlxdWVfbmFtZSI6ImRhbWlhIiwibmJmIjoxNjUwNjQ5ODI1LCJleHAiOjE2NjY0NjEwMjUsImlhdCI6MTY1MDY0OTgyNSwiaXNzIjoiQXNwTmV0Q29yZURldkp3dHMiLCJhdWQiOiJodHRwczovL2xvY2FsaG9zdDo3MTU3In0.oHulBChuI3h2hSiDAnDA7pRNT1fkl9jglNZoq64d0Qo
    ```
1. Run the `SampleWebApi` project (if in Visual Studio, set it as the starting project then hit Ctrl+F5 to launch without debugging):

    ```shell
    AspNetCoreDevJwts\SampleWebApi> dotnet run
    Building...
    info: Microsoft.Hosting.Lifetime[14]
          Now listening on: https://localhost:7157
    info: Microsoft.Hosting.Lifetime[14]
          Now listening on: http://localhost:5157
    info: Microsoft.Hosting.Lifetime[0]
          Application started. Press Ctrl+C to shut down.
    info: Microsoft.Hosting.Lifetime[0]
          Hosting environment: Development
    info: Microsoft.Hosting.Lifetime[0]
          Content root path: ~\AspNetCoreDevJwts\SampleWebApi
    ```
1. Open a browser and navigate to [`https://localhost:7157/swagger`](https://localhost:7157/swagger) to bring up the Swagger UI for the app.

    <img width="946" alt="image" src="https://user-images.githubusercontent.com/249088/164768929-12d5b281-2637-432e-a224-8f29e60e1ddb.png">

1. Use the Swagger UI to make a request to both endpoints. Note that you can successfully hit the first endpoint as it doesn't require any authorization, but the second endpoint responds with an `HTTP 401`.

    <img width="925" alt="image" src="https://user-images.githubusercontent.com/249088/164771472-eaec65e0-b42b-47de-83b3-90bf18fc7426.png">

    <img width="926" alt="image" src="https://user-images.githubusercontent.com/249088/164771385-f71694d4-49b8-4a70-b9c5-5aa755bd9b7c.png">


1. Make a GET request to `https://localhost:7157/protected` with the JWT you created before in the authorization header:

    ### Using curl
    ```shell
    ~> curl https://localhost:7157/protected -H "Authorization: Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJ1bmlxdWVfbmFtZSI6ImRhbWlhIiwibmJmIjoxNjUwNjQ5ODI1LCJleHAiOjE2NjY0NjEwMjUsImlhdCI6MTY1MDY0OTgyNSwiaXNzIjoiQXNwTmV0Q29yZURldkp3dHMiLCJhdWQiOiJodHRwczovL2xvY2FsaG9zdDo3MTU3In0.oHulBChuI3h2hSiDAnDA7pRNT1fkl9jglNZoq64d0Qo"
    Hello damia
    ~> 
    ```
    
    ### Using Postman
    **NOTE**: You might have to configure Postman to allow the ASP.NET Core local development HTTPS certificate
    
    **Setting up the request**
    <img width="960" alt="image" src="https://user-images.githubusercontent.com/249088/164769795-bcdffde7-10c9-42ab-9361-c483af7175b8.png">
    
    **The successful response**
    <img width="960" alt="image" src="https://user-images.githubusercontent.com/249088/164769903-f3b26b51-f580-4daf-936e-0728f29e6d08.png">

## What's next
Try exploring the CLI's commands and options to:
- Create JWTs for different users: `dev-jwt create -n TestUser`
- List the JWTs created for the current project: `dev-jwt list`
- Reset the JWT signing key for the current project: `dev-jwt key --reset`
- Create JWTs with custom claims: `dev-jwt create -n PrivilegedUser -c scope=myapi:read -c scope=myapi:write -c favoritecolor=blue`
- Try using the CLI on a new web project (not the one in this project) to see what the first-use experience is like
- Explore the code in this repo that makes this work at runtime
- Give feedback on the idea over on the [original issue](https://github.com/dotnet/aspnetcore/issues/39857)
