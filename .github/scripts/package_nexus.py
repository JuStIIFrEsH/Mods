"""Convert a released Thunderstore ZIP into a DLL-only Nexus install ZIP."""

import argparse
import json
import zipfile
from pathlib import Path


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("source", type=Path)
    parser.add_argument("destination", type=Path)
    parser.add_argument("--name", required=True)
    parser.add_argument("--version", required=True)
    parser.add_argument("--dll", required=True)
    args = parser.parse_args()

    with zipfile.ZipFile(args.source) as source:
        manifest = json.loads(source.read("manifest.json"))
        if manifest["name"] != args.name or manifest["version_number"] != args.version:
            raise ValueError("Release package manifest does not match the requested mod/version")
        dlls = [entry for entry in source.infolist() if entry.filename.rsplit("/", 1)[-1] == args.dll]
        if len(dlls) != 1:
            raise ValueError(f"Expected exactly one {args.dll}; found {len(dlls)}")
        data = source.read(dlls[0])

    args.destination.parent.mkdir(parents=True, exist_ok=True)
    install_path = f"BepInEx/plugins/JuStIIFrEsH-{args.name}/{args.dll}"
    with zipfile.ZipFile(args.destination, "w", compression=zipfile.ZIP_DEFLATED) as target:
        target.writestr(install_path, data)

    with zipfile.ZipFile(args.destination) as check:
        if check.namelist() != [install_path] or check.read(install_path) != data:
            raise ValueError("Nexus archive verification failed")
    print(args.destination)


if __name__ == "__main__":
    main()
