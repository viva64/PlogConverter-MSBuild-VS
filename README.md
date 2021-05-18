Plog Converter for Windows
===============================
![Platforms](https://img.shields.io/badge/platform-windows-green)

**Note**. This page is about the tool for Windows. The appropriate page about the tool for Linux and macOS [is available here](https://github.com/viva64/plog-converter).

To convert the analyzer bug report to different formats (xml, tasks and so on) you can use the Plog Converter.
It is applicable for working scenario with MSBuild\Visual Studio projects on Windows (C++, C#).

More detailed description is available on the [documentation page](https://pvs-studio.com/en/docs/manual/0038/), section "Converting the analysis results".

Getting up and running
----------------------

The steps below will take you through cloning your own repository, then compiling and running the utility yourself:

1. Install Visual Studio 2017. All desktop editions of Visual Studio 2017 can build this utility.
2. Install PVS-Studio for Windows (for installation and working of the PlogConverter tool, some binary files from the PVS-Studio installation folder are required).
   You can use the [PVS-Studio download page](https://pvs-studio.com/en/pvs-studio/download/).
3. Open PlogConverter.sln and build Release.
4. After compiling finishes, you can run the following command in Command Prompt:

```
PlogConverter.exe --help
```
