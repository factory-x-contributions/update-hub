THISDIR := justfile_directory()

# lint
lint:
  pre-commit run --all-files

# update dependencies
update:
  pre-commit autoupdate

# build
build:
  dotnet build

# run
run:
  dotnet run
