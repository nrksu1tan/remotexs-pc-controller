<div align="center">

# RemoteXS PC Controller 🎛️

**Control your PC seamlessly from your phone via Wi-Fi.** *Lightweight. Self-hosted. No app installation required.*

![Version](https://img.shields.io/badge/version-v2.4-blue?style=flat-square)
![License](https://img.shields.io/badge/license-MIT-green?style=flat-square)
![Platform](https://img.shields.io/badge/platform-Windows%2010%2F11-blue?style=flat-square)
![Tech](https://img.shields.io/badge/built%20with-.NET%208%20WPF-purple?style=flat-square)

</div>

---

## 📸 Screenshots

| **PC Server Interface** | **Mobile Interface (Media)** | **Mobile Interface (Keyboard)** |
|:---:|:---:|:---:|
| <img src="https://raw.githubusercontent.com/nrksu1tan/remotexs-pc-controller/refs/heads/main/XrdRemote/preview/11.png" width="400" alt="PC Server App"> | <img src="https://raw.githubusercontent.com/nrksu1tan/remotexs-pc-controller/refs/heads/main/XrdRemote/preview/222.png" width="200" alt="Mobile Media Control"> | <img src="https://raw.githubusercontent.com/nrksu1tan/remotexs-pc-controller/refs/heads/main/XrdRemote/preview/333.png" width="200" alt="Mobile Keyboard Control"> |

---

## ✨ Features

### 🔒 **Security & Connection**
* **Smart Auth:** Secure 4-digit PIN system.
* **Auto-Discovery:** Built-in LAN scanner finds your PC automatically without QR codes.
* **Real-Time Sync:** Powered by WebSockets for zero-latency UI updates.
* **Anti-Bruteforce:** Rate-limiting and session whitelisting.

### 🎵 **Media Control & Timeline**
* **Now Playing:** High-res album art and track info (via Windows Media API).
* **Timeline Scrubbing:** Smoothly seek through tracks in real-time.
* **Immersive UI:** Apple Music-style dynamic background blur and glassmorphism.

### 🎚️ **Live Application Mixer**
* **Per-App Volume:** Granular control over specific apps (Chrome, Spotify, Games).
* **2-Way Sync:** Phone sliders update instantly if volume is changed on the PC.

### 🖱️ **Smart Trackpad & Haptics**
* **Tactile Feedback:** Native iOS haptics (Light/Medium/Heavy) for physical-like clicks.
* **Gestures:** 2-finger scrolling, hold for right-click.

### 📋 **Productivity Tools**
* **Clipboard Sync:** 2-way text copy/paste between your PC and smartphone.
* **Presentation Mode:** Swipe gestures to change slides, quick actions (F5, Esc, Blank screen).

### 💻 **System Integration**
* **PWA Ready:** Works directly in the browser, feels like a native app.
* **System Tray:** Minimizes quietly to the tray area to keep your desktop clean.
* **Portable:** Runs as a single `.exe` file. No installation wizard.

---

## 🚀 Getting Started

### Prerequisites
* **OS:** Windows 10 or Windows 11.
* **Network:** Your PC and Smartphone must be connected to the **same Wi-Fi network**.

### 📥 Installation (User)

1. Download the latest `XrdRemote.exe` from the [Releases page](#).
2. Run the application on your PC.
3. Open the app on your phone (via QR scan or browser) and enter the **4-digit PIN** displayed on your PC, or use the **Scan Network** button to connect automatically.

> [!WARNING]
> **First Run:** When prompted, you **must allow access** in the Windows Firewall popup (check "Private Networks") so your phone can connect to the PC.

### 👨‍💻 Installation (Developer)

1. Clone the repository:
   ```bash
   git clone [https://github.com/nrksu1tan/remotexs-pc-controller.git](https://github.com/nrksu1tan/remotexs-pc-controller.git)
   ```
2. Open the solution in **Visual Studio** or **VS Code**.
3. Restore dependencies:
   ```bash
   dotnet restore
   ```
4. Run the project:
   ```bash
   dotnet run
   ```

---

## 🛠️ Tech Stack

* **Backend:** C# (.NET 8.0 WPF)
* **Networking:** `HttpListener` & `WebSockets` (Self-hosted, Zero dependencies)
* **Frontend:** HTML5, CSS3, Vanilla JS
* **Audio API:** `NAudio` (CoreAudioApi)
* **Media API:** `Windows.Media.Control` (WinRT)
* **Visuals & Haptics:** Pure inline SVGs, `ios-vibrator-pro-max`

---

## 📦 Building for Release

To build a standalone single-file executable (no .NET runtime required on the target machine):

```bash
dotnet publish -c Release -r win-x64 /p:PublishSingleFile=true /p:IncludeNativeLibrariesForSelfExtract=true
```
