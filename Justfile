set quiet

root_folder := "src"
solution := root_folder / "SourceGeneratorFramework.slnx"
build_configuration := "Debug"
artifacts_folder := "./artifacts"
default_test_filter := "/*/*/*/*/"

pipeline_feed := "https://api.nuget.org/v3/index.json"
pipeline_tool := ".tools/purview-build/purview-build"

current_version := `node -p "require('./package.json').version"`

[private]
default:
    just --list

# Install the shared Purview.Build tool (authenticated to the Purview-Dev feed) if not present
[private]
ensure-pipeline-tool:
    if [ ! -x "{{ pipeline_tool }}" ]; then \
        dotnet tool install Purview.Build --tool-path .tools/purview-build --add-source "{{ pipeline_feed }}"; \
    fi

# Run the PR pipeline (restore, build, lint, tests)
[group('Pipeline')]
pipeline-pr *args:
    just ensure-pipeline-tool
    echo "Running PR pipeline..."
    "{{ pipeline_tool }}" {{ args }}

# Run the build pipeline (restore, build, lint)
[group('Pipeline')]
pipeline-build *args:
    just ensure-pipeline-tool
    echo "Running build pipeline..."
    "{{ pipeline_tool }}" --Build:RunTests=false --Release:Mode=None {{ args }}

# Run the release pipeline (restore, build, lint, tests, pack, publish, GitHub release)
[group('Pipeline')]
pipeline-release *args:
    just ensure-pipeline-tool
    echo "Running release pipeline..."
    "{{ pipeline_tool }}" --Release:Mode=NuGet {{ args }}

# Run the release pipeline (restore, build, lint, tests, pack, local nuget publish)
# Note: `just` runs recipes through the shell, which strips backslashes from unquoted arguments.
# Use the LOCAL_NUGET_FEED_PATH environment variable or forward slashes, e.g.
# just pipeline-local-release --PublishLocalNuGet:LocalFeedPath=p:/_sync-projects/.local-nuget/
[group('Pipeline')]
pipeline-local-release *args:
    just ensure-pipeline-tool
    just lint-fix
    echo "Running local release pipeline..."
    "{{ pipeline_tool }}" --Release:Mode=LocalNuGet {{ args }}

# Run the pipeline with tests enabled
[group('Pipeline')]
pipeline-tests *args:
    just ensure-pipeline-tool
    echo "Running tests pipeline..."
    "{{ pipeline_tool }}" --Build:RunTests=true --Release:Mode=None {{ args }}

# Build and test with the specified configuration, defaulting to "Release"
[group('Build and Test')]
build solutionOrProject=solution configuration=build_configuration:
    echo "Building {{ BLUE }}{{ solutionOrProject }}{{ NORMAL }} with configuration {{ YELLOW }}{{ configuration }}{{ NORMAL }}"
    dotnet build {{ solutionOrProject }} -c {{ configuration }}

# Run tests with the specified configuration, defaulting to "Release"
[group('Build and Test')]
test solutionOrProject=solution configuration=build_configuration filter=default_test_filter *args:
    echo "Running tests for {{ BLUE }}{{ solutionOrProject }}{{ NORMAL }} with configuration {{ YELLOW }}{{ configuration }}{{ NORMAL }} and filter {{ GREEN }}{{ filter }}{{ NORMAL }}"
    dotnet test {{ solutionOrProject }} -c {{ configuration }} --treenode-filter "{{ filter }}" {{ args }}

# Clean all projects with the specified configuration, defaulting to "Release"
[group('Build and Test')]
clean solutionOrProject=solution configuration=build_configuration *args:
    echo "Cleaning {{ BLUE }}{{ solutionOrProject }}{{ NORMAL }} with configuration {{ YELLOW }}{{ configuration }}{{ NORMAL }}"
    dotnet clean {{ solutionOrProject }} -c {{ configuration }} {{ args }}

# Clean all projects, across Debug and Release configurations
[group('Build and Test')]
clean-all *args:
    echo "Cleaning all projects with configuration"
    dotnet clean {{ solution }} -c Release {{ args }}
    dotnet clean {{ solution }} -c Debug {{ args }}

# Run tests with the specified configuration, defaulting to "Release"
[group('Build and Test')]
restore solutionOrProject=solution:
    echo "Restoring dependencies for {{ BLUE }}{{ solutionOrProject }}{{ NORMAL }}"
    dotnet restore {{ solutionOrProject }}

# Create NuGet package for the project
[group('Build and Test')]
pack solutionOrProject=solution configuration=build_configuration publish_folder=artifacts_folder:
    echo "Packing {{ BLUE }}{{ solutionOrProject }}{{ NORMAL }} with configuration {{ YELLOW }}{{ configuration }}{{ NORMAL }} to {{ GREEN }}{{ publish_folder }}{{ NORMAL }}"
    dotnet pack {{ solutionOrProject }} -c {{ configuration }} -o {{ publish_folder }}

# Display the current version of the project
[group('Build and Test')]
version:
    echo "Current version: {{ GREEN }}{{ current_version }}{{ NORMAL }}"

# Open the solution in Visual Studio/ Registered application
[group('Utilities')]
vs:
    open {{ solution }}

# Check code formatting using CSharpier
[group('Utilities')]
lint-check:
    dotnet csharpier check .
    # dotnet format --verify-no-changes {{ solution }}

# Fix code formatting issues using CSharpier
[group('Utilities')]
lint-fix:
    dotnet csharpier format .
    # dotnet format {{ solution }}

# Clean up the repository by removing build artifacts, bin/obj folders etc, and shutting down the build server
[group('Utilities')]
scrub:
    find . -type d \( -name bin -o -name obj \) -exec rm -rf {} +
    just clean
    just restore --force-evaluate
    dotnet build-server shutdown
