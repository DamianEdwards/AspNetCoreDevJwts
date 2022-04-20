# AspNetCoreDevJwts
Experimenting with ideas associated with https://github.com/dotnet/aspnetcore/issues/39857

1. Open the solution in Visual Studio
1. Set the startup project to `SampleWebApi`
1. Run the app (F5 or Ctrl+F5)
1. Hit the `/devjwt/create` endpoint from the Swagger UI to create a JWT, example payload:
    ``` json
    {
      "name": "myusername",
      "claims": {
        "scope": "myapi:read myapi:write"
      }
    }
    ```
1. Copy the JWT from the response
1. Using a tool like Postman, make a POST request to `/protected` with your JWT in the authorization header

![Animation](https://user-images.githubusercontent.com/249088/164313637-0ccb8bd4-2bda-4f19-aee1-1de7e391fefc.gif)
