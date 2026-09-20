set windows-shell := ["powershell.exe", "-NoLogo", "-NoProfile", "-Command"]

mods := data_directory() / "VintagestoryData" / "Mods"

default: deploy

# Validate JSON, publish Release and zip it into Releases/
build:
    dotnet run --project CakeBuild/CakeBuild.csproj

test:
    dotnet test VSSiding.sln

# Rewrite the committed shape files from WallShapeGen's element table
[unix]
shapes:
    SIDING_REGEN=1 dotnet test VSSiding.sln

# Rewrite the committed shape files from WallShapeGen's element table
[windows]
shapes:
    $env:SIDING_REGEN=1; dotnet test VSSiding.sln

# Build, then replace the installed zip in the game's Mods folder
[unix]
deploy: build
    rm -f "{{mods}}"/vssiding_*.zip
    cp Releases/vssiding_*.zip "{{mods}}/"

# Build, then replace the installed zip in the game's Mods folder
[windows]
deploy: build
    Remove-Item "{{mods}}/vssiding_*.zip" -ErrorAction Ignore
    Copy-Item Releases/vssiding_*.zip "{{mods}}"
