{pkgs ? import <nixpkgs> {}}:
with pkgs;
  mkShell {
    nativeBuildInputs = [
      (python3.withPackages (ps: with ps; [pyyaml]))
      git
      just

      mono
      msbuild
      dotnet-sdk_8

      pre-commit
      shellcheck
      prettier
      markdownlint-cli
    ];

    shellHook = ''
    '';
  }
