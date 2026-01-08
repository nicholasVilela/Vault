## Setup CLI

To build and install the Vault CLI locally, run the following command from the project root:

```bash
dotnet pack -c Release; dotnet tool uninstall -g vault; dotnet tool install -g vault --add-source .\bin\Release
