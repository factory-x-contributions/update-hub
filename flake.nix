{
  inputs = {
    nixpkgs.url = "github:NixOS/nixpkgs/nixpkgs-unstable";
    flake-utils.url = "github:numtide/flake-utils";
    # Used by shell.nix as a compat shim.
    flake-compat = {
      url = "github:edolstra/flake-compat";
      flake = false;
    };
  };

  outputs =
    {
      self,
      nixpkgs,
      flake-utils,
      flake-compat,
    }:
    # "nix develop" and "nix-shell" give you a dev env.
    flake-utils.lib.eachDefaultSystem (
      system:
      let
        pkgs = nixpkgs.legacyPackages.${system};
      in
      {
        devShell = pkgs.mkShell {
          # The Nix packages provided in the environment
          packages = with pkgs; [
            (python3.withPackages (ps: with ps; [ pyyaml ]))
            dotnet-sdk_9
            git
            just
            hurl
            httpie
            pkgs.dotnetCorePackages.sdk_9_0

            pre-commit
            shellcheck
            nodePackages.prettier
            markdownlint-cli
          ];
          shellHook = ''echo UpdateHub Dev Environment Ready'';
        };
      }
    );
}
