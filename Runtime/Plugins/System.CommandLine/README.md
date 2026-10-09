# System.CommandLine

- Package: System.CommandLine 2.0.12, published by Microsoft.
- Source: https://www.nuget.org/packages/System.CommandLine/2.0.12
- Download: https://api.nuget.org/v3-flatcontainer/system.commandline/2.0.12/system.commandline.2.0.12.nupkg
- Imported files: `lib/netstandard2.0/System.CommandLine.dll` and `System.CommandLine.xml`.
- Package SHA-256: `49409A28E62B0AF7F0DC98D07098725EBC9769AF1AA3B06672D19F62847BCEE9`.
- License: MIT, reproduced in `LICENSE.md`.

The license text follows the package's MIT declaration, with the copyright notice from the package metadata and DLL assembly attribute.

English help/error resources are embedded in the main assembly. Optional localized satellite assemblies are not included.

The package declares System.Memory >= 4.5.5 for .NET Standard 2.0 consumers. This project's .NET Standard 2.1/CoreCLR runtime provides these APIs, so a separate System.Memory.dll is not installed.

To update, retrieve an official package, replace the DLL/XML from `lib/netstandard2.0`, retain their `.meta` files and recheck compilation, parsing, help and completions in Unity. Update this version, hash and license information too.
