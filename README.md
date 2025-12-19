<div align="center">

# RemoteXS PC Controller 🎛️

**Control your PC seamlessly from your phone via Wi-Fi.** *Lightweight. Self-hosted. No app installation required.*

![Version](https://img.shields.io/badge/version-v1.0-blue?style=flat-square)
![License](https://img.shields.io/badge/license-MIT-green?style=flat-square)
![Platform](https://img.shields.io/badge/platform-Windows%2010%2F11-blue?style=flat-square)
![Tech](https://img.shields.io/badge/built%20with-.NET%206%20WPF-purple?style=flat-square)

</div>

---

## 📸 Screenshots

| **PC Server Interface** | **Mobile Interface (Media)** | **Mobile Interface (Mixer)** |
|:---:|:---:|:---:|
| <img src="https://raw.githubusercontent.com/nrksu1tan/remotexs-pc-controller/refs/heads/main/XrdRemote/preview/111.png" width="400" alt="PC Server App"> | <img src="https://raw.githubusercontent.com/nrksu1tan/remotexs-pc-controller/refs/heads/main/XrdRemote/preview/222.png" width="200" alt="Mobile Media Control"> | <img src="https://raw.githubusercontent.com/nrksu1tan/remotexs-pc-controller/refs/heads/main/XrdRemote/preview/333.png" width="200" alt="Mobile Mixer Control"> |

---

## ✨ Features

### 🎵 **Media Control**
* **Now Playing:** Visualizes current track title, artist, and high-res album art (fetched via Windows Media API).
* **Playback:** Play, Pause, Next, Previous track controls.
* **System Audio:** Master volume slider and instant Mute toggle.
* **Immersive UI:** Apple-style background blur transitions based on album art.

### 🎚️ **Application Mixer**
* **Per-App Volume:** Granular control over specific apps (Chrome, Spotify, Discord, Games, etc.).
* **Live Icons:** Automatically fetches and renders icons for active processes.

### 🖱️ **Mouse & Trackpad**
* **Multi-touch Trackpad:** Smooth cursor movement with 1:1 responsiveness.
* **Gestures:** Two-finger scrolling (Natural Scrolling supported).
* **Mouse Emulation:** Dedicated Left and Right click buttons.

### 💻 **System Integration**
* **PWA Ready:** Works directly in the browser (Chrome/Safari).
* **System Tray:** Minimizes quietly to the tray area to keep your desktop clean.
* **Quick Pairing:** Instant connection via QR Code scanning.
* **Portable:** Runs as a single `.exe` file. No installation wizard.

---

## 🚀 Getting Started

### Prerequisites
* **OS:** Windows 10 or Windows 11.
* **Network:** Your PC and Smartphone must be connected to the **same Wi-Fi network**.

### 📥 Installation (User)

1.  Download the latest `XrdRemote.exe` from the [Releases page](#).
2.  Run the application on your PC.
3.  **Scan the QR code** shown in the app with your phone's camera.

> [!WARNING]
> **First Run:** When prompted, you **must allow access** in the Windows Firewall popup (check "Private Networks") so your phone can connect to the PC.

### 👨‍💻 Installation (Developer)

1.  Clone the repository:
    ```bash
    git clone [https://github.com/nrksu1tan/remotexs-pc-controller.git](https://github.com/nrksu1tan/remotexs-pc-controller.git)
    ```
2.  Open the solution in **Visual Studio** or **VS Code**.
3.  Restore dependencies:
    ```bash
    dotnet restore
    ```
4.  Run the project:
    ```bash
    dotnet run
    ```

---

## 🛠️ Tech Stack

* **Backend:** C# (.NET 6.0 WPF)
* **Web Server:** `HttpListener` (Self-hosted, Zero dependencies)
* **Frontend:** HTML5, CSS3 (Glassmorphism UI), Vanilla JS
* **Audio API:** `NAudio` (CoreAudioApi)
* **Media API:** `Windows.Media.Control` (WinRT)
* **Visuals:** Lucide Icons

---

## 📦 Building for Release

To build a standalone single-file executable (no .NET runtime required on the target machine):

```bash
dotnet publish -c Release -r win-x64 /p:PublishSingleFile=true /p:IncludeNativeLibrariesForSelfExtract=true
