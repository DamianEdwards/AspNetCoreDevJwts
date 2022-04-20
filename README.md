# AspNetCoreDevJwts
Experimenting with ideas associated with https://github.com/dotnet/aspnetcore/issues/39857


## Install pre-requisites

1. Install [.NET 7 Preview 3 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/7.0)

    You can use [winget](https://docs.microsoft.com/en-us/windows/package-manager/winget/):
    ```
    winget install --id=Microsoft.dotnetPreview  -e
    ```
2. Install [Visual Studio 2022 Preview](https://visualstudio.microsoft.com/vs/preview#download-preview)
    ```
    winget install --id=Microsoft.VisualStudio.2022.Community-Preview  -e
    ```

    ASP.NET and web development **must be installed even if you have a stable version of Visual Studio with the listed components**:

    <img width="319" alt="image" src="https://user-images.githubusercontent.com/45293863/164279629-1955ec11-0780-4705-88b4-d541874a4a02.png">


## Get Started

1. Open the solution in Visual Studio 2022 Preview
2. Set the startup project to `SampleWebApi`
3. Run the app (F5 or Ctrl+F5)
4. Hit the `/devjwt/create` endpoint from the Swagger UI to create a JWT, example payload:
    ``` json
    {
      "name": "myusername",
      "claims": {
        "scope": "myapi:read myapi:write"
      }
    }
    ```
5. Copy the JWT from the response
6. Using a tool like Postman, make a POST request to `/protected` with your JWT in the authorization header