# Microphone Volume Enforcer

## Introduction
Microphone Volume Enforcer is a tool i designed to automatically adjust and maintain the volume level of your microphone. This ensures consistent audio levels during calls, recordings, and other audio applications, without risking your volume being lowered by other programs not adhering to the exclusive control access in Windows 11.

## Dependencies
The project relies on several NuGet packages to function correctly. Below is a list of all dependencies and the commands to install them.

### .NET 8.0 SDK
You can download it from [here](https://dotnet.microsoft.com/en-us/download/dotnet/8.0).

### Required NuGet Packages
- **CoreAudio**: A .NET library for managing audio devices.
- **Topshelf**: A framework for hosting services.

### Installation Commands
To install the required NuGet packages, run the following commands in your project directory:

```sh
dotnet add package CoreAudio -v 1.40.0
dotnet add package Topshelf
```

## Getting Started
1. Clone the repository:
    ```sh
    git clone https://github.com/yourusername/MicrophoneVolumeEnforcer.git
    ```
2. Navigate to the project directory:
    ```sh
    cd MicrophoneVolumeEnforcer
    ```
3. Install the required NuGet packages:
    ```sh
    dotnet restore
    ```
4. Ensure the global json has this:
```sh
dotnet new globaljson --sdk-version 8.0.100
```

## Usage
To run the application, use the following command:
```sh
dotnet run
```
To build and run the application as a .exe

First run the build command
```sh
dotnet build
```
```sh
dotnet publish -c Release -r win-x64 --self-contained -p:PublishSingleFile=true --output ./publish
```

## Contributing
Contributions are welcome! Please fork the repository and submit a pull request.

## License
This project is licensed under the MIT License.