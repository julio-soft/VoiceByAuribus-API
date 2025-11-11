# Desde la raíz del workspace:

# Compilar toda la solución (API + Lambda)
dotnet build VoiceByAuribus-API.sln

# Ejecutar todos los tests (API + Lambda)
dotnet test VoiceByAuribus-API.sln

# Limpiar toda la solución
dotnet clean VoiceByAuribus-API.sln

# Listar proyectos en la solución
dotnet sln VoiceByAuribus-API.sln list

# Tests específicos del Lambda
dotnet test VoiceByAuribus.AudioUploadNotifier/test/VoiceByAuribus.AudioUploadNotifier.Tests/VoiceByAuribus.AudioUploadNotifier.Tests.csproj

# Ejecutar solo el proyecto API
dotnet run --project VoiceByAuribus.API/VoiceByAuribus-API.csproj