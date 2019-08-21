# tServerApi-Plugins
A repository containing [any] TAPI plugins.

## Compiling from source
While this may sound complicated, don't worry because each computer with the .NET Framework about version 4.0 will have the C# compiler stored in [usually] the same location.

Within each plugin root folder is a little .cmd file that, when run alongside the .cs source file, will compile it into the desired .dll format.

Given, there's a requirement for each plugin to compile successfully, and that is the tShock plugin libraries. This is included in every tShock .zip file: 

Include OTAPI.dll, TerrariaServer.exe, and TShockAPI.dll in the same folder as the plugin .cs and compile.cmd files.

A more thorough process of this can be found at the WIki.
